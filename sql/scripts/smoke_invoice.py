# -*- coding: utf-8 -*-
import json, urllib.request, urllib.error

BASE = "http://localhost:5266"

def call(method, path, body=None, token=None):
    url = BASE + path
    data = json.dumps(body, ensure_ascii=False).encode("utf-8") if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return r.status, json.loads(r.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8")
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, {"raw": raw}

def show(step, status, resp):
    msg = resp.get("message") if isinstance(resp, dict) else resp
    print(f"[{step}] HTTP={status} code={resp.get('code') if isinstance(resp, dict) else '?'} msg={msg}")
    return resp

# 登录拿 token
st, r = call("POST", "/api/auth/login", {"username": "admin", "password": "admin123", "captchaId": "x", "captchaCode": "x"})
TOKEN = r["data"].get("accessToken", "")
print("login:", st, "token_len:", len(TOKEN))

# 1) 未登录
st, r = call("GET", "/api/invoice/list")
show("1 未登录列表", st, r)

MEMBER = 1
# 2) 客户不存在
st, r = call("POST", "/api/invoice/info", {"Id":0,"CustomerId":999999,"TitleType":2,"TitleName":"某某科技有限公司","TaxNo":"91310000MA1K35RJ7B"}, TOKEN)
show("2 抬头-客户不存在", st, r)

# 3) 税号长度不合法
st, r = call("POST", "/api/invoice/info", {"Id":0,"CustomerId":MEMBER,"TitleType":2,"TitleName":"某某科技有限公司","TaxNo":"12345"}, TOKEN)
show("3 抬头-税号长度错", st, r)

# 4) 抬头名称为空
st, r = call("POST", "/api/invoice/info", {"Id":0,"CustomerId":MEMBER,"TitleType":2,"TitleName":"","TaxNo":"91310000MA1K35RJ7B"}, TOKEN)
show("4 抬头-名称为空", st, r)

# 5) 正常保存（设为默认）
st, r = call("POST", "/api/invoice/info", {"Id":0,"CustomerId":MEMBER,"TitleType":2,"TitleName":"上海镜速订科技有限公司","TaxNo":"91310000MA1K35RJ7B","BankName":"中国工商银行上海分行","BankAccount":"6222021234567890123","Address":"上海市浦东新区世纪大道100号","Phone":"021-58888888","IsDefault":1}, TOKEN)
show("5 抬头-保存成功", st, r)

# 6) 查询该客户抬头列表
st, r = call("GET", f"/api/invoice/info/{MEMBER}", None, TOKEN)
cnt = len(r["data"]) if r.get("code") == 200 and r.get("data") else 0
show(f"6 抬头-列表({cnt}条)", st, r)

# 7) 申请开票：订单 9001 待付款
st, r = call("POST", "/api/invoice/apply", {"OrderId":9001,"InvoiceType":1,"TitleType":2,"TitleName":"上海镜速订科技有限公司","TaxNo":"91310000MA1K35RJ7B"}, TOKEN)
show("7 申请-订单未支付", st, r)

# 8) 申请开票：税号不合法
st, r = call("POST", "/api/invoice/apply", {"OrderId":9003,"InvoiceType":1,"TitleType":2,"TitleName":"上海镜速订科技有限公司","TaxNo":"ABC"}, TOKEN)
show("8 申请-税号不合法", st, r)

# 9) 申请开票：专票缺开户行
st, r = call("POST", "/api/invoice/apply", {"OrderId":9003,"InvoiceType":2,"TitleType":2,"TitleName":"上海镜速订科技有限公司","TaxNo":"91310000MA1K35RJ7B"}, TOKEN)
show("9 申请-专票缺开户行", st, r)

# 10) 申请开票：金额超过订单实付
st, r = call("POST", "/api/invoice/apply", {"OrderId":9003,"InvoiceType":1,"TitleType":2,"TitleName":"上海镜速订科技有限公司","TaxNo":"91310000MA1K35RJ7B","TotalAmount":9999}, TOKEN)
show("10 申请-金额超实付", st, r)

# 11) 申请开票：正常（金额自动取订单实付 300）
st, r = call("POST", "/api/invoice/apply", {"OrderId":9003,"InvoiceType":1,"TitleType":2,"TitleName":"上海镜速订科技有限公司","TaxNo":"91310000MA1K35RJ7B","Remark":"冒烟测试"}, TOKEN)
show("11 申请-正常", st, r)
INV_ID = r["data"]["InvoiceId"] if r.get("code") == 200 and r.get("data") else 0
if r.get("data"):
    d = r["data"]
    print(f"     金额={d['TotalAmount']} 税额={d['TaxAmount']} 状态={d['StatusName']} 需人工开票={d['NeedManualIssue']}")

# 12) 重复申请
st, r = call("POST", "/api/invoice/apply", {"OrderId":9003,"InvoiceType":1,"TitleType":2,"TitleName":"上海镜速订科技有限公司","TaxNo":"91310000MA1K35RJ7B"}, TOKEN)
show("12 申请-重复", st, r)

# 13) 回写票号：号码非数字
st, r = call("PUT", f"/api/invoice/{INV_ID}/issue", {"InvoiceNo":"ABC123","InvoiceDate":"2026-09-28"}, TOKEN)
show("13 回写-号码非数字", st, r)

# 14) 回写票号：正常
st, r = call("PUT", f"/api/invoice/{INV_ID}/issue", {"InvoiceCode":"033002100311","InvoiceNo":"20260928001","InvoiceDate":"2026-09-28"}, TOKEN)
show("14 回写-正常", st, r)

# 15) 重复回写
st, r = call("PUT", f"/api/invoice/{INV_ID}/issue", {"InvoiceNo":"20260928002"}, TOKEN)
show("15 回写-重复", st, r)

# 16) 红冲：缺少蓝票号
st, r = call("PUT", f"/api/invoice/{INV_ID}/cancel", {"RedReason":"开票信息有误，需要重开"}, TOKEN)
show("16 红冲-缺蓝票号", st, r)

# 17) 红冲：正常
st, r = call("PUT", f"/api/invoice/{INV_ID}/cancel", {"RedReason":"开票信息有误，需要重开","BlueInvoiceNo":"20260928001"}, TOKEN)
show("17 红冲-正常", st, r)

# 18) 红冲：幂等
st, r = call("PUT", f"/api/invoice/{INV_ID}/cancel", {"RedReason":"开票信息有误，需要重开","BlueInvoiceNo":"20260928001"}, TOKEN)
show("18 红冲-幂等", st, r)

# 19) 列表
st, r = call("GET", "/api/invoice/list?page=1&pageSize=10", None, TOKEN)
total = r["data"]["total"] if r.get("code") == 200 and r.get("data") else 0
show(f"19 列表(共{total}条)", st, r)

# 20) 详情
st, r = call("GET", f"/api/invoice/{INV_ID}", None, TOKEN)
show("20 详情", st, r)
if r.get("data"):
    d = r["data"]
    print(f"     订单号={d['OrderNo']} 实付={d['OrderPayAmount']} 状态={d['StatusName']} 发票号={d['InvoiceNo']} 蓝票={d['BlueInvoiceNo']} 原因={d['RedReason']}")
