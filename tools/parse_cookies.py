# -*- coding: utf-8 -*-
"""CEF/Chromium cookie v10 解密（AES-GCM + DPAPI，Windows，零第三方依赖）。"""
import base64
import ctypes
import json
import os
import re
import sqlite3
import sys
import urllib.parse
from ctypes import wintypes

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# ---------------- DPAPI ----------------
def dpapi_unprotect(data: bytes) -> bytes:
    class DATA_BLOB(ctypes.Structure):
        _fields_ = [("cbData", wintypes.DWORD),
                    ("pbData", ctypes.POINTER(ctypes.c_byte))]

    crypt32 = ctypes.WinDLL("crypt32.dll", use_last_error=True)
    buf = ctypes.create_string_buffer(bytes(data), len(data))
    blob_in = DATA_BLOB(len(data), ctypes.cast(buf, ctypes.POINTER(ctypes.c_byte)))
    blob_out = DATA_BLOB()
    if not crypt32.CryptUnprotectData(ctypes.byref(blob_in), None, None, None,
                                      None, 0, ctypes.byref(blob_out)):
        raise OSError(ctypes.get_last_error())
    out = ctypes.string_at(blob_out.pbData, blob_out.cbData)
    ctypes.WinDLL("kernel32.dll").LocalFree(blob_out.pbData)
    return out

# ---------------- BCrypt AES-GCM ----------------
def aes_gcm_decrypt(key: bytes, nonce: bytes, ciphertext: bytes, tag: bytes) -> bytes:
    bcrypt = ctypes.WinDLL("bcrypt.dll", use_last_error=True)

    class BCRYPT_AUTH_CIPHER(ctypes.Structure):
        _fields_ = [
            ("cbSize", wintypes.ULONG),
            ("dwInfoVersion", wintypes.ULONG),
            ("pbNonce", ctypes.POINTER(ctypes.c_ubyte)),
            ("cbNonce", wintypes.ULONG),
            ("pbAuthData", ctypes.POINTER(ctypes.c_ubyte)),
            ("cbAuthData", wintypes.ULONG),
            ("pbTag", ctypes.POINTER(ctypes.c_ubyte)),
            ("cbTag", wintypes.ULONG),
            ("pbMacContext", ctypes.POINTER(ctypes.c_ubyte)),
            ("cbMacContext", wintypes.ULONG),
            ("cbAAD", wintypes.ULONG),
            ("cbData", ctypes.c_ulonglong),
            ("dwFlags", wintypes.ULONG),
        ]

    def bptr(b: bytes):
        a = (ctypes.c_ubyte * len(b)).from_buffer_copy(b)
        return ctypes.cast(a, ctypes.POINTER(ctypes.c_ubyte))

    h_alg = wintypes.HANDLE()
    if bcrypt.BCryptOpenAlgorithmProvider(ctypes.byref(h_alg), "AES", None, 0) != 0:
        raise OSError("BCryptOpenAlgorithmProvider")
    try:
        mode = ctypes.create_unicode_buffer("ChainingModeGCM")
        bcrypt.BCryptSetProperty(h_alg, "ChainingMode", mode, 64, 0)

        h_key = wintypes.HANDLE()
        kbuf = bptr(key)
        if bcrypt.BCryptGenerateSymmetricKey(h_alg, ctypes.byref(h_key), None, 0,
                                             kbuf, len(key), 0) != 0:
            raise OSError("BCryptGenerateSymmetricKey")
        try:
            auth = BCRYPT_AUTH_CIPHER()
            auth.cbSize = ctypes.sizeof(BCRYPT_AUTH_CIPHER)
            auth.dwInfoVersion = 1
            auth.pbNonce = bptr(nonce)
            auth.cbNonce = len(nonce)
            auth.pbTag = bptr(tag)
            auth.cbTag = len(tag)
            auth.pbAuthData = bptr(b"")
            auth.cbAuthData = 0
            auth.cbAAD = 0

            cbuf = bptr(ciphertext)
            out = (ctypes.c_ubyte * len(ciphertext))()
            res = wintypes.ULONG()
            status = bcrypt.BCryptDecrypt(
                h_key, cbuf, len(ciphertext), ctypes.byref(auth),
                None, 0, ctypes.byref(out), len(ciphertext), ctypes.byref(res), 0)
            if status != 0:
                raise OSError(f"BCryptDecrypt status=0x{status:x}")
            return bytes(out[:res.value])
        finally:
            bcrypt.BCryptDestroyKey(h_key)
    finally:
        bcrypt.BCryptCloseAlgorithmProvider(h_alg, 0)


def decrypt_v10(enc: bytes, key: bytes) -> bytes:
    """v10 = 'v10' + nonce(12) + ciphertext + tag(16)"""
    if not enc.startswith(b"v10") or len(enc) < 3 + 12 + 16 + 1:
        raise ValueError("非 v10 加密")
    nonce = enc[3:15]
    ciphertext = enc[15:-16]
    tag = enc[-16:]
    return aes_gcm_decrypt(key, nonce, ciphertext, tag)


def load_key(userdata: str) -> bytes:
    prefs_path = None
    for name in ("LocalPrefs.json", "Local State"):
        p = __import__("os").path.join(userdata, name)
        if __import__("os").path.exists(p):
            prefs_path = p
            break
    if not prefs_path:
        raise FileNotFoundError("未找到 LocalPrefs.json / Local State")
    raw = json.load(open(prefs_path, encoding="utf-8"))
    b64 = raw["os_crypt"]["encrypted_key"]
    blob = __import__("base64").b64decode(b64)
    if blob[:5] == b"DPAPI":
        blob = blob[5:]
    return dpapi_unprotect(blob)


if __name__ == "__main__":
    ud = sys.argv[1]
    key = load_key(ud)

    con = sqlite3.connect(os.path.join(ud, "Cookies"))
    con.text_factory = bytes
    cur = con.cursor()
    cur.execute("SELECT host_key, name, encrypted_value FROM cookies")

    cookies = {}
    rows = []
    for hk, nm, ev in cur.fetchall():
        if not ev:
            continue
        name = nm.decode(errors="replace")
        try:
            val = decrypt_v10(bytes(ev), key).decode("utf-8", errors="replace")
        except Exception as e:
            val = f"<解密失败 {e}>"
        cookies[name] = val
        rows.append((hk.decode(errors="replace"), name, val))

    def unescape_js_unicode(s: str) -> str:
        return re.sub(r"%u([0-9a-fA-F]{4})",
                      lambda m: chr(int(m.group(1), 16)), s)

    print("=== 区服信息 ===")
    print("uin        :", cookies.get("uin", "（无）"))
    print("sServerID  :", cookies.get("sServerID", "（无）"))
    print("sServerName:", unescape_js_unicode(cookies.get("sServerName", "")) or "（无）")

    tll = cookies.get("tmpLastLoginInfo")
    if tll:
        try:
            obj = json.loads(urllib.parse.unquote(tll))
            print("tmpLastLoginInfo:", json.dumps(obj, ensure_ascii=False))
            zones = set()
            for p in obj.get("playerlist", []):
                zones.update(p.get("zonelist", []))
            if zones:
                print("上次登录区服    :", "、".join(str(z) for z in zones))
        except Exception as e:
            print("tmpLastLoginInfo 解析失败:", e)
    else:
        print("tmpLastLoginInfo:（无，账号未完成过登录）")

    missing = [k for k in ("skey", "p_skey", "access_token", "openid")
               if k not in cookies]
    if missing:
        print("\n提示: 缺少登录 cookie:", "、".join(missing),
              "→ 无有效登录态，启动会走账号密码登录")

    print("\n=== 全部 cookie ===")
    for hk, nm, val in rows:
        print(f"{hk:30} {nm:22} = {val[:120]}")