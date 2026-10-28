"""Leitor mínimo de GLB (malhas, pesos de pele, hierarquia de ossos); sem dependências além do numpy."""
import json
import struct
import numpy as np

_CT = {5120: 'i1', 5121: 'u1', 5122: 'i2', 5123: 'u2', 5125: 'u4', 5126: 'f4'}
_NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


def load_glb(path):
    d = open(path, 'rb').read()
    assert d[:4] == b'glTF'
    o = 12; js = None; blob = None
    while o < len(d):
        ln, ty = struct.unpack_from('<II', d, o)
        chunk = d[o + 8:o + 8 + ln]; o += 8 + ln
        if ty == 0x4E4F534A: js = json.loads(chunk)
        elif ty == 0x004E4942: blob = chunk
    return js, blob


def accessor(js, blob, i):
    a = js['accessors'][i]; bv = js['bufferViews'][a['bufferView']]
    n = _NC[a['type']]; dt = np.dtype(_CT[a['componentType']])
    off = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    stride = bv.get('byteStride') or n * dt.itemsize
    if stride == n * dt.itemsize:
        return np.frombuffer(blob, dtype=dt, count=a['count'] * n, offset=off).reshape(a['count'], n)
    return np.stack([np.frombuffer(blob, dtype=dt, count=n, offset=off + k * stride) for k in range(a['count'])])


def skin_info(js, blob, skin_index=0):
    """Nomes dos ossos, pai de cada osso (índice na lista ou -1) e posição de repouso (cabeça) em espaço do modelo."""
    sk = js['skins'][skin_index]; nodes = js['nodes']
    ibm = np.stack([m.T for m in accessor(js, blob, sk['inverseBindMatrices']).reshape(-1, 4, 4)])
    joints = sk['joints']; pos_of = {j: k for k, j in enumerate(joints)}
    parent_node = {}
    for i, nd in enumerate(nodes):
        for c in nd.get('children', []): parent_node[c] = i
    names = [nodes[j]['name'] for j in joints]
    parents = [pos_of.get(parent_node.get(j), -1) for j in joints]
    heads = np.stack([np.linalg.inv(m)[:3, 3] for m in ibm])
    return names, parents, heads
