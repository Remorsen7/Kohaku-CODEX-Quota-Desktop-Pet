"""Local stdio launcher bridge. No network or transcript access."""
import ctypes
from ctypes import wintypes
import json
import os
from pathlib import Path
import subprocess
import sys

EXE=Path(__file__).resolve().parent/'app'/'CodexQuotaPet.exe'

def is_running():
    kernel=ctypes.WinDLL('kernel32',use_last_error=True)
    kernel.OpenMutexW.argtypes=[wintypes.DWORD,wintypes.BOOL,wintypes.LPCWSTR]
    kernel.OpenMutexW.restype=wintypes.HANDLE
    kernel.CloseHandle.argtypes=[wintypes.HANDLE]
    handle=kernel.OpenMutexW(0x00100000,False,'Local\\CodexQuotaPet-v1')
    if not handle:return False
    kernel.CloseHandle(handle)
    return True

def ensure_started():
    if is_running():return 'already_running'
    if not EXE.is_file():raise FileNotFoundError('Bundled desktop pet is missing')
    # Delegate to the user's Windows shell so MCP job cleanup cannot kill the pet.
    exe=str(EXE).replace("'","''")
    folder=str(EXE.parent).replace("'","''")
    script=f"$ErrorActionPreference='Stop'; $shell=New-Object -ComObject Shell.Application; $loc=0; $root=0; $hwnd=0; $desktop=$shell.Windows().FindWindowSW([ref]$loc,[ref]$root,8,[ref]$hwnd,1); if($null -eq $desktop){{throw 'Windows desktop shell unavailable'}}; $desktop.Document.Application.ShellExecute('{exe}','','{folder}','open',1)"
    subprocess.run([str(Path(os.environ.get('SystemRoot','C:/Windows'))/'System32/WindowsPowerShell/v1.0/powershell.exe'),
        '-NoProfile','-NonInteractive','-WindowStyle','Hidden','-Command',script],
        stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,stderr=subprocess.PIPE,
        creationflags=subprocess.CREATE_NO_WINDOW,check=True,timeout=10)
    return 'started'

def dispatch(request, starter):
    if not isinstance(request,dict):
        return {'jsonrpc':'2.0','id':None,'error':{'code':-32600,'message':'Invalid request'}}
    if 'id' not in request:return None
    base={'jsonrpc':'2.0','id':request['id']}
    method=request.get('method')
    params=request.get('params') or {}
    if not isinstance(params,dict):return {**base,'error':{'code':-32602,'message':'Invalid parameters'}}
    if method=='initialize':
        result={'protocolVersion':params.get('protocolVersion','2025-06-18'),
            'serverInfo':{'name':'quota_pet','version':'1.0.0'},'capabilities':{'tools':{'listChanged':False}}}
        try:starter()
        except Exception as exc:
            print('Quota pet startup failed: '+repr(exc),file=sys.stderr,flush=True)
            result['instructions']='本机额度桌宠启动失败；需要时请调用 quota_pet_start 重试。'
        return {**base,'result':result}
    if method=='ping':return {**base,'result':{}}
    if method=='tools/list':
        return {**base,'result':{'tools':[{'name':'quota_pet_start','title':'启动额度桌宠',
            'description':'仅当用户明确要求启动或重新启动本机额度桌宠时调用。插件加载时已自动启动；不读取聊天，不上传数据。',
            'inputSchema':{'type':'object','properties':{},'additionalProperties':False},
            'annotations':{'readOnlyHint':False,'destructiveHint':False,'idempotentHint':True,'openWorldHint':False}}]}}
    if method=='tools/call':
        if params.get('name')!='quota_pet_start':return {**base,'error':{'code':-32601,'message':'Unknown tool'}}
        if params.get('arguments') not in ({},None):return {**base,'error':{'code':-32602,'message':'This tool accepts no arguments'}}
        try:
            state=starter()
            result={'content':[{'type':'text','text':'额度桌宠已在运行。' if state=='already_running' else '已启动本机额度桌宠。'}]}
        except Exception:
            result={'isError':True,'content':[{'type':'text','text':'启动失败，请检查本地桌宠安装。'}]}
        return {**base,'result':result}
    return {**base,'error':{'code':-32601,'message':'Method not found'}}

def main():
    sys.stdin.reconfigure(encoding='utf-8')
    sys.stdout.reconfigure(encoding='utf-8')
    for line in sys.stdin:
        try:response=dispatch(json.loads(line),ensure_started)
        except json.JSONDecodeError:response={'jsonrpc':'2.0','id':None,'error':{'code':-32700,'message':'Parse error'}}
        if response is not None:
            sys.stdout.write(json.dumps(response,ensure_ascii=False)+'\n')
            sys.stdout.flush()

if __name__=='__main__':main()


