"""Leitor/escritor do formato BGM1 (Assets/Resources/Models/*.bytes), espelho de Core/MeshStore.cs.

Por malha: nome, n vértices, flags (1 = posição/normal, 2 = cor, 4 = UV2 só em x, 8 = pesos de osso),
posições (n*3 f32), normais (n*3 f32), cores (n*4 f32), uv2.x (n f32), pesos (4 u8 índices + 4 f32 por vértice)
e submalhas (nome do material, cor RGBA, índices i32).
"""
import struct
import numpy as np


def read_bgm(path):
    d = open(path, 'rb').read()
    o = 4
    assert d[:4] == b'BGM1', 'formato inválido'

    def i32():
        nonlocal o
        v = struct.unpack_from('<i', d, o)[0]; o += 4; return v

    def f32(n):
        nonlocal o
        a = np.frombuffer(d, dtype='<f4', count=n, offset=o).copy(); o += 4 * n; return a

    def string():
        nonlocal o
        n = i32(); v = d[o:o + n].decode('utf-8'); o += n; return v

    out = {}
    for _ in range(i32()):
        name = string(); n = i32(); flags = i32()
        m = {'flags': flags}
        m['pos'] = f32(n * 3).reshape(n, 3)
        m['nrm'] = f32(n * 3).reshape(n, 3)
        if flags & 2: m['col'] = f32(n * 4).reshape(n, 4)
        if flags & 4: m['uv2'] = f32(n)
        if flags & 8:
            raw = np.frombuffer(d, dtype=np.dtype([('b', 'u1', 4), ('w', '<f4', 4)]), count=n, offset=o); o += n * 20
            m['bi'] = raw['b'].copy(); m['bw'] = raw['w'].copy()
        subs = []
        for _ in range(i32()):
            mn = string(); c = f32(4); ni = i32()
            idx = np.frombuffer(d, dtype='<i4', count=ni, offset=o).copy(); o += 4 * ni
            subs.append((mn, c, idx))
        m['subs'] = subs
        out[name] = m
    return out


def write_bgm(path, meshes):
    parts = [b'BGM1', struct.pack('<i', len(meshes))]

    def string(s):
        b = s.encode('utf-8'); return struct.pack('<i', len(b)) + b

    for name, m in meshes.items():
        n = len(m['pos'])
        flags = 1 | (2 if 'col' in m else 0) | (4 if 'uv2' in m else 0) | (8 if 'bi' in m else 0)
        parts += [string(name), struct.pack('<ii', n, flags)]
        parts.append(np.asarray(m['pos'], '<f4').tobytes())
        parts.append(np.asarray(m['nrm'], '<f4').tobytes())
        if 'col' in m: parts.append(np.asarray(m['col'], '<f4').tobytes())
        if 'uv2' in m: parts.append(np.asarray(m['uv2'], '<f4').tobytes())
        if 'bi' in m:
            rec = np.zeros(n, dtype=np.dtype([('b', 'u1', 4), ('w', '<f4', 4)]))
            rec['b'] = m['bi']; rec['w'] = m['bw']
            parts.append(rec.tobytes())
        parts.append(struct.pack('<i', len(m['subs'])))
        for mn, c, idx in m['subs']:
            parts += [string(mn), np.asarray(c, '<f4').tobytes(), struct.pack('<i', len(idx)), np.asarray(idx, '<i4').tobytes()]
    open(path, 'wb').write(b''.join(parts))
