"""Create separate source and Windows release archives using only stdlib."""
from pathlib import Path
import hashlib
import zipfile

root=Path(__file__).resolve().parents[1]
version=(root/'VERSION').read_text(encoding='utf-8').strip()
name=f'CodexQuotaPet-v{version}'
out=root/'dist/releases'
out.mkdir(parents=True,exist_ok=True)
skip={'bin','obj','__pycache__','.git','.vs','.vscode','build','dist','app'}
def source_files():
    for p in sorted(root.rglob('*')):
        rel=p.relative_to(root)
        if p.is_file() and not any(x in skip for x in rel.parts) and p.suffix.lower() not in {'.pdb','.pyc','.log','.zip','.user'}:
            yield p,rel
sources=list(source_files())
sourcezip=out/f'{name}-source.zip'
with zipfile.ZipFile(sourcezip,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
    for p,rel in sources:archive.write(p,f'{name}/{rel.as_posix()}')
exe=root/'dist/app/CodexQuotaPet.exe'
if not exe.is_file():raise SystemExit('Run scripts/build.ps1 before packaging')
winzip=out/f'{name}-windows-x64.zip'
with zipfile.ZipFile(winzip,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
    for p,rel in sources:
        if rel.parts[0] in {'assets','plugin','docs'} or rel.as_posix() in {'VERSION','README.md','LICENSE.md','THIRD_PARTY_NOTICES.md','CHANGELOG.md','scripts/install.ps1'}:
            archive.write(p,f'{name}/{rel.as_posix()}')
    archive.write(exe,f'{name}/app/CodexQuotaPet.exe')
checks=[]
for p in (sourcezip,winzip):
    checks.append(f'{hashlib.file_digest(p.open("rb"),"sha256").hexdigest()}  {p.name}')
    print(f'{p.name}: {p.stat().st_size:,} bytes')
(out/f'SHA256SUMS-v{version}.txt').write_text('\n'.join(checks)+'\n',encoding='utf-8')
