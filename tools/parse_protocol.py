import sys, re
sys.stdout.reconfigure(encoding='utf-8', errors='replace')

# 火影忍者OL Socket 协议解析器
# 用法: python tools/parse_protocol.py <pktmon hex txt>
# 前置: pktmon filter add naruto -p 10741; pktmon start --capture
#        pktmon etl2txt <etl> -o <hex.txt> --hex   (UTF-16LE 输出)
#
# 协议结构（请求，52 字节）:
#   [0:2]  09 01              协议标识
#   [2:4]  00 10              总长度
#   [4:6]  00 03 / 00 10      框架类型
#   [6:8]  XX XX              功能号 sub-command
#   [8:12] 00 00 00           保留
#   [12]   XX                  命令序号（会话内递增）
#   [13:17]00 00 00 01         方向/请求标记
#   [17:24]...                 会话标识
#   [24:28]2b 3c 08 87 00 00 22 98  服务器会话标识

def parse_hex_file(path):
    lines = open(path, encoding='utf-16-le', errors='replace').readlines()
    packets = []
    current = None
    hex_pat = re.compile(r'0x[0-9a-f]+:\s*([0-9a-f ]+)')
    for line in lines:
        m = re.search(r'183\.194\.190\.49\.10741 > 10\.211\.55\.3\.62994:.*length (\d+)', line)
        m2 = re.search(r'10\.211\.55\.3\.62994 > 183\.194\.190\.49\.10741:.*length (\d+)', line)
        if m:
            current = {'dir': 'RX', 'ip_len': int(m.group(1)), 'bytes': b''}
            packets.append(current)
            continue
        if m2:
            current = {'dir': 'TX', 'ip_len': int(m2.group(1)), 'bytes': b''}
            packets.append(current)
            continue
        if current is not None:
            hm = hex_pat.search(line)
            if hm:
                hexstr = hm.group(1).strip().replace(' ', '')
                if hexstr:
                    current['bytes'] += bytes.fromhex(hexstr)
            else:
                current = None
    return packets

def tcp_payload(b):
    if len(b) < 40:
        return b''
    ihl = (b[14] & 0x0F) * 4
    tcp_off = ((b[14 + ihl + 12] >> 4) & 0x0F) * 4
    return b[14 + ihl + tcp_off:]

def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    packets = parse_hex_file(sys.argv[1])

    # 聚合请求
    cmds = {}
    for p in packets:
        if p['dir'] != 'TX':
            continue
        pay = tcp_payload(p['bytes'])
        if len(pay) < 16 or not (pay[0] == 0x09 and pay[1] == 0x01):
            continue
        length = (pay[2] << 8) | pay[3]
        ftype = (pay[4] << 8) | pay[5]
        sub = (pay[6] << 8) | pay[7]
        seq = pay[12]
        key = (sub, ftype)
        if key not in cmds:
            cmds[key] = {'count': 0, 'seqs': [], 'len': length}
        cmds[key]['count'] += 1
        if seq not in cmds[key]['seqs']:
            cmds[key]['seqs'].append(seq)

    print(f"=== 功能号分布（{len(cmds)} 种请求）===")
    for (sub, ftype), v in sorted(cmds.items(), key=lambda x: x[0][0]):
        print(f"  sub=0x{sub:04X} type=0x{ftype:04X}: {v['count']} 次, 序号 {[f'{s:02X}' for s in v['seqs']]}")

if __name__ == '__main__':
    main()
