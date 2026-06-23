#!/usr/bin/env python
"""
blender_mcp_send.py — send a command to the running BlenderMCP (ahujasid) addon
over its TCP socket (default localhost:9876).

Usage:
  python blender_mcp_send.py code <path-to-python-file>   # execute_blender_code from file
  python blender_mcp_send.py scene                        # get_scene_info
  python blender_mcp_send.py shot <out.png> [max_size]    # get_viewport_screenshot -> save to out.png

This is the transport the Far Horizon weapon-set modeling uses when the
mcp__blender__* tools are not wired into the agent session. Registered in
tools/debug/REGISTRY.md.
"""
import socket, json, sys, base64, os

HOST, PORT = 'localhost', 9876


def send(cmd, recv_timeout=120):
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.settimeout(15)
    s.connect((HOST, PORT))
    s.sendall(json.dumps(cmd).encode())
    s.settimeout(recv_timeout)
    data = b''
    while True:
        try:
            chunk = s.recv(65536)
        except socket.timeout:
            break
        if not chunk:
            break
        data += chunk
        try:
            json.loads(data.decode())
            break
        except Exception:
            continue
    s.close()
    return json.loads(data.decode())


def main():
    if len(sys.argv) < 2:
        print('usage: code <file> | scene | shot <out.png> [max]')
        sys.exit(2)
    mode = sys.argv[1]
    if mode == 'scene':
        print(json.dumps(send({'type': 'get_scene_info', 'params': {}}), indent=2))
    elif mode == 'code':
        with open(sys.argv[2], 'r', encoding='utf-8') as f:
            code = f.read()
        resp = send({'type': 'execute_code', 'params': {'code': code}})
        print(json.dumps(resp, indent=2)[:6000])
    elif mode == 'shot':
        out = sys.argv[2]
        max_size = int(sys.argv[3]) if len(sys.argv) > 3 else 1200
        resp = send({'type': 'get_viewport_screenshot',
                     'params': {'max_size': max_size, 'filepath': os.path.abspath(out),
                                'format': 'png'}}, recv_timeout=60)
        print(json.dumps(resp, indent=2)[:1500])
    else:
        print('unknown mode', mode)
        sys.exit(2)


if __name__ == '__main__':
    main()
