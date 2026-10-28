#!/usr/bin/env python
"""Gera Assets/Resources/Models/human_v2.bytes: corpo novo (male base mesh CC0) retargetado para o esqueleto físico.

Entradas
  --base  male-base-mesh-base_male.glb   (malha de pele + rig de 163 ossos, pose A, 1,786 m)
  --old   Assets/Resources/Models/human.bytes   (órgãos, ossos, cabelo e mãos do viewmodel são copiados daqui)
  --anat  Assets/Scripts/Data/AnatomyData.cs    (posições das partes e âncoras das juntas físicas)
Saída
  human_v2.bytes  - mesmo formato BGM1 do human.bytes; o original NÃO é tocado.

Método: cada osso do rig novo é fundido na parte física correspondente (17 partes). Cada parte recebe uma
transformação de segmento (rotação + escala axial + escala perpendicular) que leva as juntas do rig novo
(ombro, cotovelo, punho, quadril, joelho, tornozelo, coluna...) exatamente sobre as âncoras dos ConfigurableJoint.
A pose é "assada" na malha: a pele fica no repouso do projeto (braços caídos) e os pivôs das articulações
coincidem com as juntas físicas, então não há fenda nem escorregamento ao dobrar.
"""
import argparse
import re
import numpy as np

from bgm import read_bgm, write_bgm
from gltf_min import load_glb, accessor, skin_info

ORDER = ["pelvis", "abdomen", "chest", "neck", "head",
         "upper_arm_l", "forearm_l", "hand_l", "upper_arm_r", "forearm_r", "hand_r",
         "thigh_l", "shin_l", "foot_l", "thigh_r", "shin_r", "foot_r"]
IDX = {p: i for i, p in enumerate(ORDER)}

SKIN = np.array([0.60, 0.40, 0.30])      # mesma paleta do corpo antigo
SHIRT = np.array([0.30, 0.30, 0.25])
SHORTS = np.array([0.05, 0.05, 0.10])


def parse_anatomy(path):
    s = open(path, encoding='utf-8').read()
    parts = {}
    for blk in re.split(r'var p = new PartDef', s)[1:]:
        pid = re.search(r'id = "(\w+)"', blk).group(1)
        pos = np.array([float(x) for x in re.search(r'pos = new Vector3\(([^)]*)\)', blk).group(1).replace('f', '').split(',')])
        j = re.search(r'p\.joint = new JointDef \{[^}]*?anchor = new Vector3\(([^)]*)\)', blk)
        anchor = np.array([float(x) for x in j.group(1).replace('f', '').split(',')]) if j else None
        parts[pid] = (pos, anchor)
    return parts


def joint_part(name):
    n = name.lower()
    side = '_l' if n.endswith('.l') else '_r' if n.endswith('.r') else ''
    if n in ('root', 'spine05') or n.startswith('pelvis'): return 'pelvis'
    if n in ('spine04', 'spine03'): return 'abdomen'
    if n in ('spine02', 'spine01') or n.startswith(('breast', 'clavicle', 'shoulder')): return 'chest'
    if n.startswith('neck'): return 'neck'
    if n.startswith('upperarm'): return 'upper_arm' + side
    if n.startswith('lowerarm'): return 'forearm' + side
    if n.startswith(('wrist', 'metacarpal', 'finger')): return 'hand' + side
    if n.startswith('upperleg'): return 'thigh' + side
    if n.startswith('lowerleg'): return 'shin' + side
    if n.startswith(('foot', 'toe')): return 'foot' + side
    return 'head'          # cabeça, mandíbula, língua, olhos, músculos faciais


def rot_from_to(a, b):
    a = a / np.linalg.norm(a); b = b / np.linalg.norm(b)
    v = np.cross(a, b); c = float(np.dot(a, b))
    if c < -0.999999:
        ax = np.cross(a, [1, 0, 0])
        if np.linalg.norm(ax) < 1e-6: ax = np.cross(a, [0, 1, 0])
        ax /= np.linalg.norm(ax)
        return 2 * np.outer(ax, ax) - np.eye(3)
    k = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
    return np.eye(3) + k + k @ k / (1 + c)


class Segment:
    """v' = J1a + k * dot(v-J0a, u0) * u1 + sp * R @ perp(v-J0a)."""
    def __init__(self, j0a, j0b, j1a, j1b, sp, k=None):
        self.j0a, self.j1a, self.sp = j0a, j1a, sp
        a = j0b - j0a; b = j1b - j1a
        self.u0 = a / np.linalg.norm(a); self.u1 = b / np.linalg.norm(b)
        self.k = np.linalg.norm(b) / np.linalg.norm(a) if k is None else k
        self.R = rot_from_to(self.u0, self.u1)

    def apply(self, v):
        d = v - self.j0a
        ax = d @ self.u0
        perp = d - np.outer(ax, self.u0)
        return self.j1a + self.k * np.outer(ax, self.u1) + self.sp * (perp @ self.R.T)

    def apply_n(self, n):
        return n @ self.R.T


def along(points, d):
    """Ponto a distância d (arco) do início da polilinha `points`."""
    for a, b in zip(points[:-1], points[1:]):
        L = np.linalg.norm(b - a)
        if d <= L: return a + (b - a) * (d / L)
        d -= L
    return points[-1] + (points[-1] - points[-2]) / np.linalg.norm(points[-1] - points[-2]) * d


def slide_knots(src, tgt, t):
    """Pontos-fonte das juntas intermediárias.

    src/tgt: juntas do rig novo / âncoras físicas (mesmo número, em ordem ao longo da cadeia).
    t = 0: o pivô cai exatamente na junta verdadeira do modelo (pode esticar muito um segmento);
    t = 1: a distorção axial é igual em todos os segmentos (o pivô se afasta da junta verdadeira).
    """
    src = [np.asarray(p, float) for p in src]; tgt = [np.asarray(p, float) for p in tgt]
    Ls = sum(np.linalg.norm(b - a) for a, b in zip(src[:-1], src[1:]))
    Lt = sum(np.linalg.norm(b - a) for a, b in zip(tgt[:-1], tgt[1:]))
    out = [src[0]]
    d_true = 0.0; d_eq = 0.0
    for i in range(1, len(src) - 1):
        d_true += np.linalg.norm(src[i] - src[i - 1])
        d_eq += np.linalg.norm(tgt[i] - tgt[i - 1]) / Lt * Ls
        out.append(along(src, (1 - t) * d_true + t * d_eq))
    out.append(src[-1])
    return out


def build_segments(heads, names, anat, s, t=0.6):
    h = {n: heads[i] for i, n in enumerate(names)}
    A = lambda pid: anat[pid][1]
    P = lambda pid: anat[pid][0]
    seg = {}
    # coluna: quadril -> abdômen -> tórax -> pescoço -> cabeça
    hips = (h['upperleg01.L'] + h['upperleg01.R']) / 2
    hips_t = (A('thigh_l') + A('thigh_r')) / 2
    chain_src = [hips, h['spine04'], h['spine02'], h['neck01'], h['head']]
    chain_tgt = [hips_t, A('abdomen'), A('chest'), A('neck'), A('head')]
    k = slide_knots(chain_src, chain_tgt, t)
    for i, pid in enumerate(['pelvis', 'abdomen', 'chest', 'neck']):
        seg[pid] = Segment(k[i], k[i + 1], chain_tgt[i], chain_tgt[i + 1], s)
    top = h['head'] + np.array([0, 0.152, -0.007])
    seg['head'] = Segment(h['head'], top, A('head'), A('head') + np.array([0, 0.146, 0]), s)
    for sd, L in (('_l', 'L'), ('_r', 'R')):
        # braço: ombro -> cotovelo -> punho
        ks = slide_knots([h['upperarm01.' + L], h['lowerarm01.' + L], h['wrist.' + L]], [A('upper_arm' + sd), A('forearm' + sd), A('hand' + sd)], t)
        seg['upper_arm' + sd] = Segment(ks[0], ks[1], A('upper_arm' + sd), A('forearm' + sd), s)
        seg['forearm' + sd] = Segment(ks[1], ks[2], A('forearm' + sd), A('hand' + sd), s)
        fingers = h['finger3-1.' + L]
        hd = P('hand' + sd) - A('hand' + sd)
        tgt = A('hand' + sd) + hd / np.linalg.norm(hd) * np.linalg.norm(fingers - h['wrist.' + L]) * s
        seg['hand' + sd] = Segment(ks[2], ks[2] + (fingers - h['wrist.' + L]), A('hand' + sd), tgt, s, k=s)
        # perna: quadril -> joelho -> tornozelo
        kl = slide_knots([h['upperleg01.' + L], h['lowerleg01.' + L], h['foot.' + L]], [A('thigh' + sd), A('shin' + sd), A('foot' + sd)], t)
        seg['thigh' + sd] = Segment(kl[0], kl[1], A('thigh' + sd), A('shin' + sd), s)
        seg['shin' + sd] = Segment(kl[1], kl[2], A('shin' + sd), A('foot' + sd), s)
        toe = h['toe3-1.' + L]
        seg['foot' + sd] = Segment(kl[2], kl[2] + (toe - h['foot.' + L]), A('foot' + sd), A('foot' + sd) + (toe - h['foot.' + L]) * s, s, k=s)
    return seg


def read_prim(js, blob, mesh_i, prim_i):
    p = js['meshes'][mesh_i]['primitives'][prim_i]; at = p['attributes']
    pos = accessor(js, blob, at['POSITION']).astype(np.float64)
    nrm = accessor(js, blob, at['NORMAL']).astype(np.float64)
    ji = accessor(js, blob, at['JOINTS_0']).astype(int)
    jw = accessor(js, blob, at['WEIGHTS_0']).astype(np.float64)
    if jw.max() > 1.5: jw = jw / 65535.0 if jw.max() > 255 else jw / 255.0
    idx = accessor(js, blob, p['indices']).astype(np.int64).reshape(-1)
    return pos, nrm, ji, jw, idx


def noise(p, f=60.0, seed=0.0):
    """Ruído suave em [-1, 1], com direções oblíquas (sem listras alinhadas aos eixos)."""
    a = p @ np.array([0.62, 0.55, 0.56]); b = p @ np.array([-0.48, 0.71, 0.52]); c = p @ np.array([0.33, -0.41, 0.85])
    return (np.sin(a * f + seed) * np.cos(b * f * 1.31 + seed * 0.7) + np.sin(c * f * 0.77 + seed * 1.9)) / 2


def paint(pos, dom, rng):
    """Pele, camisa e short por região (em espaço do corpo já retargetado)."""
    n = len(pos)
    col = np.tile(SKIN, (n, 1)) * (1 + (rng.random((n, 1)) - 0.5) * 0.06)
    y = pos[:, 1]
    d = np.array([ORDER[i] for i in dom])
    hem = 0.93 + noise(pos, 45, 1) * 0.008
    in_torso = np.isin(d, ['pelvis', 'abdomen', 'chest'])
    neckline = (y > 1.38) & (np.hypot(pos[:, 0], pos[:, 2] * 0.8) < 0.064)
    shirt = in_torso & (y > hem) & ~neckline
    sleeve_hem = 1.22 + noise(pos, 40, 3) * 0.012
    shirt |= np.isin(d, ['upper_arm_l', 'upper_arm_r']) & (y > sleeve_hem)
    shorts_hem = 0.63 + noise(pos, 40, 5) * 0.010
    shorts = np.isin(d, ['pelvis', 'abdomen', 'thigh_l', 'thigh_r']) & (y <= hem) & (y > shorts_hem)
    shorts &= ~shirt
    col[shirt] = SHIRT * (0.95 + 0.10 * rng.random((int(shirt.sum()), 1)))
    col[shorts] = SHORTS * (0.95 + 0.10 * rng.random((int(shorts.sum()), 1)))
    alpha = np.ones(n)
    alpha[shorts] = 0.5
    return col, alpha


def winding_sign(pos, nrm, idx):
    t = idx.reshape(-1, 3)
    fn = np.cross(pos[t[:, 1]] - pos[t[:, 0]], pos[t[:, 2]] - pos[t[:, 0]])
    vn = nrm[t].mean(1)
    return np.sign(np.einsum('ij,ij->i', fn, vn).mean())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--base', required=True)
    ap.add_argument('--old', required=True)
    ap.add_argument('--anat', required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--no-hair', action='store_true')
    ap.add_argument('--ply', help='grava uma prévia colorida (PLY) do corpo novo')
    a = ap.parse_args()

    anat = parse_anatomy(a.anat)
    js, blob = load_glb(a.base)
    names, parents, heads = skin_info(js, blob)
    jpart = np.array([IDX[joint_part(n)] for n in names])

    # primitivas da malha "base": 0 pele (63.750 v), 1 aréolas, 2 boca/dentes, 3 unhas das mãos, 4 unhas dos pés, 5 orelhas;
    # a malha "high-poly" é o olho
    prims = [read_prim(js, blob, 0, k) for k in range(len(js['meshes'][0]['primitives']))] + [read_prim(js, blob, 1, 0)]
    PRIM_NAMES = ['pele', 'areolas', 'boca', 'unhas_mao', 'unhas_pe', 'orelhas', 'olhos']
    offs = np.cumsum([0] + [len(p[0]) for p in prims])
    pos = np.vstack([p[0] for p in prims]); nrm = np.vstack([p[1] for p in prims])
    ji = np.vstack([p[2] for p in prims]); jw = np.vstack([p[3] for p in prims])
    idx = np.concatenate([p[4] + offs[i] for i, p in enumerate(prims)])
    prim_of = np.concatenate([np.full(len(p[0]), i) for i, p in enumerate(prims)])
    n = len(pos)
    n_body = len(prims[0][0])
    eye_sel = np.where(prim_of == 6)[0]

    # peso por parte física (soma dos ossos fundidos)
    W = np.zeros((n, len(ORDER)))
    for k in range(4):
        np.add.at(W, (np.arange(n), jpart[ji[:, k]]), jw[:, k])
    W /= W.sum(1, keepdims=True)

    s = 1.716 / 1.786                                       # altura do corpo antigo / altura do modelo novo
    seg = build_segments(heads, names, anat, s)
    newpos = np.zeros_like(pos); newnrm = np.zeros_like(nrm)
    for pi, pid in enumerate(ORDER):
        w = W[:, pi]; m = w > 1e-5
        if not m.any(): continue
        newpos[m] += w[m, None] * seg[pid].apply(pos[m])
        newnrm[m] += w[m, None] * seg[pid].apply_n(nrm[m])
    newnrm /= np.maximum(np.linalg.norm(newnrm, axis=1, keepdims=True), 1e-9)
    pos, nrm = newpos, newnrm

    # 4 maiores pesos, renormalizados
    order = np.argsort(-W, axis=1)[:, :4]
    bw = np.take_along_axis(W, order, 1); bw /= bw.sum(1, keepdims=True)
    dom = order[:, 0]

    rng = np.random.default_rng(7)
    col, alpha = paint(pos, dom, rng)       # alfa: 1 pele/camisa, 0,5 short; olhos mexem depois (0,25 íris, 0,9 resto)
    # olhos: esclera clara com íris escura no lado da frente
    for side in (1, -1):
        sel = eye_sel[np.sign(pos[eye_sel, 0]) == side]
        c = pos[sel].mean(0)
        dvec = pos[sel] - c; dvec /= np.linalg.norm(dvec, axis=1, keepdims=True)
        iris = dvec[:, 2] > 0.55
        pupil = dvec[:, 2] > 0.93
        col[sel] = np.where(pupil[:, None], np.array([0.02, 0.02, 0.02]), np.where(iris[:, None], np.array([0.22, 0.14, 0.08]), np.array([0.80, 0.70, 0.62])))
        alpha[sel] = np.where(iris, 0.25, 0.9)          # 0,25 íris (cor por boneco), 0,9 esclera/pupila (fixas)

    # pequenos detalhes: aréolas mais escuras, boca, unhas
    col[prim_of == 1] = SKIN * 0.72
    col[prim_of == 2] = np.array([0.55, 0.30, 0.28])
    col[(prim_of == 3) | (prim_of == 4)] = np.array([0.78, 0.58, 0.50])

    # olhos menores (o globo do modelo fica grande em relação às pálpebras): encolhe 14% em torno do centro de cada olho
    for side in (1, -1):
        sel = eye_sel[np.sign(pos[eye_sel, 0]) == side]
        c = pos[sel].mean(0)
        pos[sel] = c + (pos[sel] - c) * 0.86

    # convenção de enrolamento: a do arquivo antigo
    old = read_bgm(a.old)
    ob = old['body']
    t_old = winding_sign(ob['pos'], ob['nrm'], ob['subs'][0][2])
    t_new = winding_sign(pos, nrm, idx)
    if t_old != t_new:
        t = idx.reshape(-1, 3).copy(); t[:, [1, 2]] = t[:, [2, 1]]; idx = t.reshape(-1)
    print(f'enrolamento antigo {t_old:+.0f} novo {t_new:+.0f} -> {"invertido" if t_old != t_new else "mantido"}')

    out = {}
    for k, m in old.items():
        if k == 'body' or k.startswith('skin_'): continue
        if a.no_hair and k.startswith('hair_'): continue
        out[k] = m
    out['body'] = {
        'pos': pos, 'nrm': nrm, 'col': np.hstack([col, alpha[:, None]]),
        'uv2': (dom + 0.5) / 32.0, 'bi': order.astype(np.uint8), 'bw': bw,
        'subs': [('skin', np.array([0.8, 0.8, 0.8, 1.0]), idx)],
    }

    # peças rígidas por parte (membros arrancados): triângulos pela parte dominante, em espaço local da parte
    tri = idx.reshape(-1, 3)
    tdom = np.array([np.bincount(dom[t], minlength=len(ORDER)).argmax() for t in tri])
    for pi, pid in enumerate(ORDER):
        sel = tri[tdom == pi]
        if len(sel) == 0: continue
        used, inv = np.unique(sel.reshape(-1), return_inverse=True)
        out['skin_' + pid] = {
            'pos': pos[used] - anat[pid][0], 'nrm': nrm[used], 'col': np.hstack([col[used], alpha[used, None]]),
            'subs': [('skin', np.array([0.8, 0.8, 0.8, 1.0]), inv.astype(np.int32))],
        }
    write_bgm(a.out, out)

    print(f'vértices {n} ({", ".join(f"{PRIM_NAMES[i]} {len(p[0])}" for i, p in enumerate(prims))}), triângulos {len(idx) // 3}')
    print(f'bbox novo   y {pos[:, 1].min():.3f}..{pos[:, 1].max():.3f}  x {pos[:, 0].min():.3f}..{pos[:, 0].max():.3f}  z {pos[:, 2].min():.3f}..{pos[:, 2].max():.3f}')
    print(f'bbox antigo y {ob["pos"][:, 1].min():.3f}..{ob["pos"][:, 1].max():.3f}  x {ob["pos"][:, 0].min():.3f}..{ob["pos"][:, 0].max():.3f}  z {ob["pos"][:, 2].min():.3f}..{ob["pos"][:, 2].max():.3f}')
    print('segmentos (k = escala axial, 1 = sem esticar):')
    for pid in ORDER:
        ang = np.degrees(np.arccos(np.clip(seg[pid].u0 @ seg[pid].u1, -1, 1)))
        print(f'  {pid:12s} k={seg[pid].k:.3f}  rotação={ang:5.1f} graus')
    print('máx de partes com peso>1e-3 por vértice:', int((W > 1e-3).sum(1).max()))
    if a.ply:
        with open(a.ply, 'w') as f:
            f.write(f'ply\nformat ascii 1.0\nelement vertex {n}\nproperty float x\nproperty float y\nproperty float z\n'
                    f'property uchar red\nproperty uchar green\nproperty uchar blue\nelement face {len(idx) // 3}\n'
                    f'property list uchar int vertex_indices\nend_header\n')
            c8 = (np.clip(col, 0, 1) * 255).astype(int)
            for p_, c_ in zip(pos, c8): f.write(f'{p_[0]:.5f} {p_[1]:.5f} {p_[2]:.5f} {c_[0]} {c_[1]} {c_[2]}\n')
            for t in idx.reshape(-1, 3): f.write(f'3 {t[0]} {t[1]} {t[2]}\n')


if __name__ == '__main__':
    main()
