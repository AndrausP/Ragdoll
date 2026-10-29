#!/usr/bin/env python
"""Acrescenta armas novas a Assets/Resources/Models/weapons.bytes (as existentes não são tocadas).

  crossbow  : besta (modelo CC0 "Wooden crossbow", Wind astella, OpenGameArt) convertida de tools/fontes/crossbow.glb
  harpoon   : arpão (arma de mergulho) feito aqui, a partir de primitivas
  bolt      : virote da besta (projétil)
  harpoon_bolt : arpão (projétil), com farpas e argola para a corda
  nailgun   : pistola de pregos (feita aqui, primitivas)
  nail      : prego (projétil)
  hatchet   : machadinha de arremesso (feita aqui; em pé em +Y como as armas brancas, gume em +X)

Convenção do arquivo (igual às armas de fogo existentes): cano em +X, topo em +Y, largura em Z.
Projéteis: comprimento em +Z com a ponta em +Z (igual à "arrow").

  python tools/add_weapons.py
"""
import math
import sys
import numpy as np

from bgm import read_bgm, write_bgm
from gltf_min import load_glb, accessor

OUT = '../Assets/Resources/Models/weapons.bytes'
FLIP = np.array([-1, 1, -1], 'f4')

COL = {
    'DarkWood': (0.16, 0.07, 0.04, 1), 'LightWood': (0.24, 0.10, 0.05, 1),
    'DarkMetal': (0.05, 0.05, 0.05, 1), 'Metal': (0.07, 0.07, 0.07, 1), 'LightMetal': (0.20, 0.20, 0.21, 1),
    'Black': (0.03, 0.03, 0.03, 1), 'Steel': (0.38, 0.40, 0.44, 1), 'LightSteel': (0.46, 0.49, 0.54, 1),
    'Red': (0.57, 0.14, 0.15, 1), 'Copper': (0.55, 0.28, 0.12, 1), 'Wood': (0.10, 0.06, 0.03, 1),
    'Orange': (0.80, 0.30, 0.05, 1),
}


class Builder:
    """Acumula geometria por material."""

    def __init__(self):
        self.mats = {}

    def _add(self, mat, v, n, t):
        m = self.mats.setdefault(mat, {'v': [], 'n': [], 't': [], 'c': 0})
        m['v'].append(np.asarray(v, 'f4')); m['n'].append(np.asarray(n, 'f4'))
        m['t'].append(np.asarray(t, 'i4') + m['c']); m['c'] += len(v)

    @staticmethod
    def _frame(a, b):
        ax = np.asarray(b, float) - np.asarray(a, float)
        ln = np.linalg.norm(ax); ax = ax / max(ln, 1e-9)
        h = np.array([0, 1, 0.0]) if abs(ax[1]) < 0.9 else np.array([1, 0, 0.0])
        u = np.cross(ax, h); u /= np.linalg.norm(u)
        w = np.cross(ax, u)
        return ax, u, w, ln

    def cone(self, mat, a, b, r0, r1, seg=14, caps=True):
        """Cilindro/cone de a (raio r0) até b (raio r1)."""
        ax, u, w, ln = self._frame(a, b)
        a = np.asarray(a, float); b = np.asarray(b, float)
        v, n = [], []
        slope = (r0 - r1) / max(ln, 1e-9)
        for k in range(seg + 1):
            th = 2 * math.pi * k / seg
            d = math.cos(th) * u + math.sin(th) * w
            nn = d + ax * slope; nn /= np.linalg.norm(nn)
            v += [a + d * r0, b + d * r1]; n += [nn, nn]
        t = []
        for k in range(seg):
            i = 2 * k
            t += [[i, i + 1, i + 3], [i, i + 3, i + 2]]
        self._add(mat, v, n, t)
        if caps:
            for end, r, sgn in ((a, r0, -1), (b, r1, 1)):
                if r <= 1e-5: continue
                base = [end + (math.cos(2 * math.pi * k / seg) * u + math.sin(2 * math.pi * k / seg) * w) * r for k in range(seg)]
                vv = [end] + base; nn = [ax * sgn] * len(vv)
                tt = [[0, 1 + k, 1 + (k + 1) % seg] if sgn > 0 else [0, 1 + (k + 1) % seg, 1 + k] for k in range(seg)]
                self._add(mat, vv, nn, tt)

    def box(self, mat, c, size, rot_z=0.0, rot_y=0.0):
        """Caixa centrada em c, com tamanho (x, y, z), girada em Z e depois em Y (graus)."""
        c = np.asarray(c, float); s = np.asarray(size, float) / 2
        cz, sz = math.cos(math.radians(rot_z)), math.sin(math.radians(rot_z))
        cy, sy = math.cos(math.radians(rot_y)), math.sin(math.radians(rot_y))
        Rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]]); Ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
        R = Ry @ Rz
        faces = [((1, 0, 0), [(1, -1, -1), (1, 1, -1), (1, 1, 1), (1, -1, 1)]),
                 ((-1, 0, 0), [(-1, -1, 1), (-1, 1, 1), (-1, 1, -1), (-1, -1, -1)]),
                 ((0, 1, 0), [(-1, 1, -1), (-1, 1, 1), (1, 1, 1), (1, 1, -1)]),
                 ((0, -1, 0), [(-1, -1, 1), (-1, -1, -1), (1, -1, -1), (1, -1, 1)]),
                 ((0, 0, 1), [(1, -1, 1), (1, 1, 1), (-1, 1, 1), (-1, -1, 1)]),
                 ((0, 0, -1), [(-1, -1, -1), (-1, 1, -1), (1, 1, -1), (1, -1, -1)])]
        v, n, t = [], [], []
        for nrm, cs in faces:
            i = len(v)
            for q in cs: v.append(c + R @ (np.array(q) * s)); n.append(R @ np.array(nrm, float))
            t += [[i, i + 1, i + 2], [i, i + 2, i + 3]]
        self._add(mat, v, n, t)

    def ring(self, mat, c, axis, R, r, seg=20, tube=8):
        """Argola (toro) de raio R e espessura r em torno de `axis` centrada em c."""
        ax = np.asarray(axis, float); ax /= np.linalg.norm(ax)
        _, u, w, _ = self._frame((0, 0, 0), ax)
        c = np.asarray(c, float)
        v, n = [], []
        for i in range(seg + 1):
            th = 2 * math.pi * i / seg
            d = math.cos(th) * u + math.sin(th) * w
            for j in range(tube + 1):
                ph = 2 * math.pi * j / tube
                nn = math.cos(ph) * d + math.sin(ph) * ax
                v.append(c + d * R + nn * r); n.append(nn)
        t = []
        for i in range(seg):
            for j in range(tube):
                a = i * (tube + 1) + j; b = a + tube + 1
                t += [[a, b, b + 1], [a, b + 1, a + 1]]
        self._add(mat, v, n, t)

    def mesh(self):
        pos, nrm, tri_by_mat = [], [], []
        off = 0
        subs = []
        for mat, m in self.mats.items():
            v = np.concatenate(m['v']); n = np.concatenate(m['n']); t = np.concatenate(m['t'])
            pos.append(v); nrm.append(n)
            subs.append((mat, np.array(COL[mat], 'f4'), (t + off).astype('i4').reshape(-1)))
            off += len(v)
        return {'flags': 1, 'pos': np.concatenate(pos), 'nrm': np.concatenate(nrm), 'subs': subs}


def harpoon_gun():
    b = Builder()
    # cano e abraçadeiras
    b.cone('DarkMetal', (-0.25, 0.0, 0), (1.62, 0.0, 0), 0.062, 0.062)
    for x in (0.15, 0.75, 1.30):
        b.cone('Metal', (x, 0, 0), (x + 0.07, 0, 0), 0.080, 0.080)
    b.cone('Black', (1.55, 0, 0), (1.68, 0, 0), 0.092, 0.092)
    # arpão carregado: haste, cabeça e farpas
    b.cone('LightSteel', (0.20, 0, 0), (2.38, 0, 0), 0.024, 0.024, seg=10)
    b.cone('Steel', (2.38, 0, 0), (2.70, 0, 0), 0.052, 0.0, seg=10)
    for rz in (28, -28):
        b.box('Steel', (2.30, 0.0, 0), (0.18, 0.012, 0.05), rot_z=0, rot_y=rz)
        b.box('Steel', (2.30, 0.0, 0), (0.18, 0.05, 0.012), rot_z=rz, rot_y=0)
    # coronha, empunhadura, gatilho
    b.box('DarkWood', (-0.42, -0.03, 0), (0.95, 0.17, 0.105))
    b.box('DarkWood', (-0.82, -0.05, 0), (0.12, 0.20, 0.10), rot_z=-8)
    b.box('DarkWood', (-0.30, -0.26, 0), (0.13, 0.36, 0.10), rot_z=14)
    b.box('DarkMetal', (-0.08, -0.17, 0), (0.28, 0.018, 0.06))
    b.box('DarkMetal', (0.05, -0.12, 0), (0.018, 0.12, 0.06))
    b.box('Metal', (-0.20, -0.14, 0), (0.025, 0.10, 0.022), rot_z=12)
    # carretel de linha e mira
    b.cone('Metal', (-0.05, 0.11, -0.11), (-0.05, 0.11, 0.11), 0.085, 0.085, seg=18)
    b.cone('Black', (-0.05, 0.11, -0.085), (-0.05, 0.11, 0.085), 0.10, 0.10, seg=18)
    b.box('DarkMetal', (0.20, 0.085, 0), (0.07, 0.04, 0.02))
    b.box('DarkMetal', (1.45, 0.095, 0), (0.03, 0.05, 0.02))
    # elásticos (borracha) do colar da boca até a empunhadura
    for z in (-0.095, 0.095):
        for y in (0.01, -0.035):
            b.cone('Black', (1.40, y, z), (-0.02, y - 0.02, z * 0.9), 0.014, 0.014, seg=8)
    return b.mesh()


def bolt():
    b = Builder()
    b.cone('LightWood', (0, 0, 0), (0, 0, 2.05), 0.022, 0.022, seg=8)
    b.cone('Steel', (0, 0, 2.05), (0, 0, 2.38), 0.048, 0.0, seg=8)
    for rot in (0, 120, 240):
        a = math.radians(rot)
        b.box('Red', (math.cos(a) * 0.04, math.sin(a) * 0.04, 0.12), (0.003, 0.08, 0.30), rot_z=rot - 90)
    return b.mesh()


def harpoon_bolt():
    b = Builder()
    b.cone('LightSteel', (0, 0, 0.1), (0, 0, 2.25), 0.030, 0.030, seg=10)
    b.cone('Steel', (0, 0, 2.25), (0, 0, 2.68), 0.062, 0.0, seg=10)
    for rot in (0, 90):
        b.box('Steel', (0, 0, 2.18), (0.16, 0.012, 0.22), rot_z=rot)
    b.ring('Black', (0, 0, 0.06), (0, 0, 1), 0.07, 0.012)
    return b.mesh()


def nailgun():
    b = Builder()
    # corpo, cilindro do pistão e bico
    b.box('Orange', (0.42, 0.20, 0), (1.10, 0.34, 0.20))
    b.box('Black', (0.42, 0.40, 0), (0.95, 0.06, 0.16))
    b.cone('DarkMetal', (0.97, 0.18, 0), (1.32, 0.18, 0), 0.085, 0.07, seg=16)
    b.cone('LightSteel', (1.32, 0.18, 0), (1.52, 0.18, 0), 0.05, 0.035, seg=12)
    b.box('Black', (1.10, 0.05, 0), (0.30, 0.10, 0.16))
    # pente de pregos inclinado sob o bico
    b.box('LightMetal', (0.78, -0.16, 0), (0.70, 0.12, 0.07), rot_z=-14)
    b.box('Steel', (0.78, -0.24, 0), (0.66, 0.05, 0.05), rot_z=-14)
    # empunhadura emborrachada, gatilho, bateria
    b.box('Black', (-0.02, -0.22, 0), (0.17, 0.52, 0.15), rot_z=12)
    b.box('DarkMetal', (0.15, -0.06, 0), (0.05, 0.14, 0.05), rot_z=12)
    b.box('Orange', (-0.10, -0.52, 0), (0.36, 0.14, 0.24))
    b.box('Black', (-0.10, -0.60, 0), (0.38, 0.03, 0.25))
    return b.mesh()


def nail():
    b = Builder()
    b.cone('Steel', (0, 0, 0.05), (0, 0, 2.0), 0.030, 0.030, seg=8)
    b.cone('Steel', (0, 0, 2.0), (0, 0, 2.25), 0.030, 0.0, seg=8)
    b.cone('LightSteel', (0, 0, 0.0), (0, 0, 0.06), 0.095, 0.095, seg=12)
    return b.mesh()


def hatchet():
    b = Builder()
    # cabo de madeira (em pé em +Y), com empunhadura enrolada
    b.cone('LightWood', (0, -1.15, 0), (0, 1.35, 0), 0.075, 0.065, seg=12)
    b.cone('Black', (0, -1.10, 0), (0, -0.35, 0), 0.085, 0.085, seg=12)
    # cabeça: olho, lâmina em leque para +X e martelo atrás
    b.box('DarkMetal', (0.0, 1.18, 0), (0.30, 0.34, 0.16))
    b.box('Steel', (0.36, 1.18, 0), (0.46, 0.30, 0.09))
    b.box('LightSteel', (0.66, 1.18, 0), (0.18, 0.62, 0.04))
    b.box('LightSteel', (0.55, 1.36, 0), (0.30, 0.10, 0.05), rot_z=-30)
    b.box('LightSteel', (0.55, 1.00, 0), (0.30, 0.10, 0.05), rot_z=30)
    b.box('DarkMetal', (-0.24, 1.18, 0), (0.16, 0.22, 0.14))
    return b.mesh()


def node_trs(nd):
    x, y, z, w = nd.get('rotation', [0, 0, 0, 1])
    R = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                  [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                  [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]], 'f4')
    return R, np.array(nd.get('translation', [0, 0, 0]), 'f4'), np.array(nd.get('scale', [1, 1, 1]), 'f4')


def crossbow():
    js, blob = load_glb('fontes/crossbow.glb')
    pos, nrm, subs = [], [], []
    off = 0
    names = {m['name'] if 'name' in m else str(i): i for i, m in enumerate(js['materials'])}
    for prim in js['meshes'][0]['primitives']:
        p = accessor(js, blob, prim['attributes']['POSITION']).astype('f4')
        n = accessor(js, blob, prim['attributes']['NORMAL']).astype('f4')
        R, T, S = node_trs(js['nodes'][0])
        p = (p * S) @ R.T + T; n = n @ R.T
        idx = accessor(js, blob, prim['indices']).astype('i4').reshape(-1)
        # o glTF tem a mola da besta em -X; o jogo quer o cano (frente) em +X: gira 180° em torno de Y
        p = p * FLIP; n = n * FLIP
        # ao espelhar dois eixos o enrolamento dos triângulos continua o mesmo (rotação própria)
        mat = js['materials'][prim['material']]
        nm = mat.get('name', 'Metal')
        key = {'Copper': 'Copper', 'Metal': 'LightMetal', 'Wood': 'DarkWood'}.get(nm, 'Metal')
        pos.append(p); nrm.append(n)
        subs.append((key, np.array(COL[key], 'f4'), (idx + off).astype('i4')))
        off += len(p)
    pos = np.concatenate(pos); nrm = np.concatenate(nrm)
    # centro na origem do eixo longo (como as outras armas): só desloca para a frente mínima em x = 0 na coronha
    return {'flags': 1, 'pos': pos, 'nrm': nrm, 'subs': subs}


def main():
    m = read_bgm(OUT)
    for name, fn in (('crossbow', crossbow), ('harpoon', harpoon_gun), ('bolt', bolt), ('harpoon_bolt', harpoon_bolt),
                     ('nailgun', nailgun), ('nail', nail), ('hatchet', hatchet)):
        mesh = fn()
        p = mesh['pos']
        print(f'{name}: {len(p)} vértices, caixa {p.min(0).round(2)} .. {p.max(0).round(2)}')
        m[name] = mesh
    write_bgm(OUT, dict(sorted(m.items())))
    print('ok ->', OUT)


if __name__ == '__main__':
    main()
