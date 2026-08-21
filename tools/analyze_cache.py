# 火影忍者OL 缓存解密分析工具
#
# 用法：python tools/analyze_cache.py <userdata_dir>
#   <userdata_dir> 为某账号的 userdata 目录（含 Cache 子目录）
#
# 原理：游戏运行时把加密的 ZWS SWF / 配置解密后加载，Chromium 会把
# 加载的资源（含解压后的明文配置）缓存到 Cache/ 目录。部分缓存条目
# 是标准 LZMA / Zlib 压缩的明文，可直接解压还原出游戏配置数据
# （忍者属性、技能、副本、装备、战斗等），无需逆向 SWF。
#
# 输出：解压文件写入 <userdata_dir>/Cache/decoded/，并在控制台列出
#       配置类数据的关键内容。

import os
import re
import sys
import zlib
import lzma


def unzip_swf(path):
    """识别并解压缓存文件，返回 (类型, 数据)。"""
    d = open(path, 'rb').read()
    if len(d) < 20:
        return None, None

    # SWF 标准压缩
    if d[:3] == b'FWS':
        return 'SWF-FWS', d
    if d[:3] == b'CWS':
        try:
            return 'SWF-CWS', zlib.decompress(d[8:])
        except Exception:
            return 'SWF-CWS', None
    if d[:3] == b'ZWS':
        return 'SWF-ZWS(加密)', None  # LZMA 自定义头，解密失败
    # 标准 LZMA alone（0x5D 属性字节 + dict + size）
    if d[0] == 0x5D and len(d) > 13:
        try:
            dec = lzma.LZMADecompressor(format=lzma.FORMAT_ALONE)
            return 'LZMA', dec.decompress(d)
        except Exception:
            return 'LZMA-头?', None
    # Zlib（0x78）
    if d[0] == 0x78:
        try:
            return 'ZLIB', zlib.decompress(d)
        except Exception:
            return 'ZLIB-头?', None
    # 图片
    if d[:4] in (b'\x89PNG', b'GIF8') or d[:3] == b'\xFF\xD8\xFF':
        return 'IMG', None
    if d[:3] == b'PK\x03':
        return 'ZIP', None
    return 'OTHER/加密', None


# 配置类数据关键字段（用于过滤展示）
CFG_KEYWORDS = re.compile(
    r'config/|\.cfg|\.xml|\.inc|\.pkg|Ninja|Skill|battle|dungeon|task|'
    r'Equipment|Card|Arena|role|user|bag|item|friend|chat|NPC', re.I)


def extract_strings(data, min_len=6):
    return [s.decode('ascii', 'replace')
            for s in re.findall(rb'[\x20-\x7e]{%d,}' % min_len, data)]


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    ud = sys.argv[1]
    cache = os.path.join(ud, 'Cache')
    if not os.path.isdir(cache):
        print(f"未找到 Cache 目录: {cache}")
        sys.exit(1)

    out = os.path.join(cache, 'decoded')
    os.makedirs(out, exist_ok=True)

    files = sorted(f for f in os.listdir(cache) if f.startswith('f_'))
    print(f"=== 共 {len(files)} 个缓存文件，开始分析 ===")

    decoded_count = 0
    cfg_hits = 0
    for fn in files:
        path = os.path.join(cache, fn)
        kind, data = unzip_swf(path)
        if not kind:
            continue
        if data is not None:
            # 保存解压结果
            ext = {'SWF-FWS': 'fws', 'SWF-CWS': 'cws', 'LZMA': 'lzma',
                   'ZLIB': 'zlib'}.get(kind, 'dat')
            save = os.path.join(out, f"{fn}.{ext}")
            open(save, 'wb').write(data)
            decoded_count += 1

            # 统计配置引用
            strings = extract_strings(data, 8)
            cfgs = [s for s in strings if CFG_KEYWORDS.search(s)]
            if cfgs:
                cfg_hits += 1
                print(f"\n--- {fn} [{kind}] {len(data)}B 含配置引用 ---")
                for s in sorted(set(cfgs))[:10]:
                    print(f"    {s[:110]}")

    print(f"\n=== 完成：解压 {decoded_count} 个，含配置 {cfg_hits} 个 ===")
    print(f"输出目录: {out}")


if __name__ == '__main__':
    main()
