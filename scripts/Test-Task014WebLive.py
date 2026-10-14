"""TASK-014 Google DEV/HTTPS/HTTP integration; all fixtures FAKE/DRAFT.
No TLS bypass, no Google credentials on the client, no raw responses/secrets in evidence.
Sessions/checkpoints are DPAPI CurrentUser outside Git and removed on successful completion.
The UAT password key is always deleted, including failure; a protected session permits reconcile/resume.
"""
import argparse, secrets, string, datetime as dt, hashlib, http.cookiejar, io, json, os, pathlib, socket, ssl, struct, subprocess, sys, time, urllib.error, urllib.parse, urllib.request, uuid, xml.etree.ElementTree as ET, zipfile, zlib
sys.stdout.reconfigure(encoding='utf-8')
ROOT=pathlib.Path(__file__).resolve().parent.parent
PRIVATE=pathlib.Path(os.environ['LOCALAPPDATA'])/'DuyTienAssets'/'Task014Live'
PRIVATE.mkdir(parents=True,exist_ok=True)
STATE_PATH=PRIVATE/'web-session.dpapi'
EVIDENCE=ROOT/'docs'/'07-testing'/'TASK-014-WEB-GOOGLE-LIVE-EVIDENCE.json'
BASE='https://localhost:7008'
KEY='UatTask005:AdminPassword'
PROJECT='src/DuyTien.AssetManagement.Server/DuyTien.AssetManagement.Server.csproj'
SID=ET.parse(ROOT/PROJECT).find('.//UserSecretsId').text
STORE=pathlib.Path(os.environ['APPDATA'])/'Microsoft'/'UserSecrets'/SID/'secrets.json'
context=ssl.create_default_context()
assert context.check_hostname and context.verify_mode==ssl.CERT_REQUIRED
class NoRedirect(urllib.request.HTTPRedirectHandler):
 def redirect_request(self,req,fp,code,msg,headers,newurl):return None
jar=http.cookiejar.CookieJar()
web=urllib.request.build_opener(NoRedirect(),urllib.request.HTTPSHandler(context=context),urllib.request.HTTPCookieProcessor(jar))
api=urllib.request.build_opener(NoRedirect(),urllib.request.HTTPSHandler(context=context))
state={};results=[];events=[];last=0;server=None

def ps(script,input=None):
 r=subprocess.run(['powershell','-NoProfile','-Command',script],input=input,capture_output=True,cwd=ROOT)
 if r.returncode:raise RuntimeError('PowerShell operation failed; output hidden')
 return r.stdout

def secret_delete():
 cfg=json.loads(STORE.read_text(encoding='utf-8-sig'))
 if KEY in cfg:
  del cfg[KEY];temp=STORE.with_name(STORE.name+'.'+uuid.uuid4().hex+'.tmp')
  try:temp.write_text(json.dumps(cfg,ensure_ascii=False,indent=2),encoding='utf-8');os.replace(temp,STORE)
  finally:
   if temp.exists():temp.unlink()
 return KEY not in json.loads(STORE.read_text(encoding='utf-8-sig'))

def save():
 payload=json.dumps(state,ensure_ascii=False).encode('utf-8')
 script="Add-Type -AssemblyName System.Security;$s=[Console]::In.ReadToEnd();$b=[Text.Encoding]::UTF8.GetBytes($s);$p=[Security.Cryptography.ProtectedData]::Protect($b,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser);[IO.File]::WriteAllBytes('"+str(STATE_PATH).replace("'","''")+"',$p)"
 ps(script,payload)

def load():
 script="Add-Type -AssemblyName System.Security;$p=[IO.File]::ReadAllBytes('"+str(STATE_PATH).replace("'","''")+"');$b=[Security.Cryptography.ProtectedData]::Unprotect($p,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser);[Console]::OutputEncoding=[Text.Encoding]::UTF8;[Console]::Write([Text.Encoding]::UTF8.GetString($b))"
 return json.loads(ps(script).decode('utf-8-sig'))

def expect(case,actual,expected):
 ok=actual==expected;entry={'case':case,'expected':expected,'actual':actual,'pass':ok,'source':'AI_RUN_OBSERVED'};results.append(entry);print(json.dumps(entry,ensure_ascii=False),flush=True)
 if not ok:raise RuntimeError('Unexpected integration result: '+case)

def call(case,method,path,payload=None,key=None,raw=None,mime=None,cookie=False,anonymous=False,expected=200,auth_token=None,html=False):
 global last
 if not anonymous and state.get('session') and path!='/api/auth/refresh':
  expiry=dt.datetime.fromisoformat(state['session']['accessExpiresAt'].replace('Z','+00:00'))
  if expiry-dt.datetime.now(dt.timezone.utc)<dt.timedelta(minutes=3):
   session=call('Runner session rotation','POST','/api/auth/refresh',{'refreshToken':state['session']['refreshToken']},anonymous=True);state['session']=session;save()
 delay=last+12-time.monotonic()
 if delay>0:time.sleep(delay)
 headers={'Accept':'text/html' if cookie or html else 'application/json'}
 if not anonymous:
  if cookie:headers['Cookie']='dt_session='+state['cookieToken']
  else:headers['Authorization']='Bearer '+state['session']['accessToken']
 if auth_token:headers['Authorization']='Bearer '+auth_token
 if key:headers['Idempotency-Key']='w14-'+state['runId']+'-'+key
 body=raw
 if payload is not None:body=json.dumps(payload,ensure_ascii=False).encode('utf-8');headers['Content-Type']='application/json'
 if mime:headers['Content-Type']=mime
 req=urllib.request.Request(BASE+path,data=body,headers=headers,method=method);last=time.monotonic()
 try:r=(web if cookie else api).open(req,timeout=180)
 except urllib.error.HTTPError as e:r=e
 with r:
  data=r.read();status=r.status;ctype=r.headers.get('Content-Type','');response_headers=dict(r.headers)
 if status!=expected and path.startswith('/api/') and status in (400,409,422,503):
  try:events.append({'event':'problem','case':case,'status':status,'title':json.loads(data).get('title','[no title]')});print(json.dumps(events[-1],ensure_ascii=False),flush=True)
  except (ValueError,AttributeError):pass
 expect(case+' HTTP',status,expected)
 if 'json' in ctype and data:return json.loads(data)
 return data,response_headers

def multipart(fields,filename,content,filemime):
 boundary='Task014'+uuid.uuid4().hex;body=bytearray()
 for k,v in fields.items():body.extend(('--'+boundary+'\r\nContent-Disposition: form-data; name="'+k+'"\r\n\r\n'+str(v)+'\r\n').encode())
 body.extend(('--'+boundary+'\r\nContent-Disposition: form-data; name="file"; filename="'+filename+'"\r\nContent-Type: '+filemime+'\r\n\r\n').encode());body.extend(content);body.extend(('\r\n--'+boundary+'--\r\n').encode());return bytes(body),'multipart/form-data; boundary='+boundary

def once(name,fn):
 if name not in state['steps']:state['steps'][name]=fn();save()
 return state['steps'][name]

def backend_count():return int(ps("@(Get-CimInstance Win32_Process | Where-Object Name -eq 'DuyTien.AssetManagement.Server.exe').Count").decode().strip())

def start_server():
 global server
 if backend_count():raise RuntimeError('Existing backend must be verified/reused; refusing second backend')
 log=open(PRIVATE/('server-'+uuid.uuid4().hex+'.log'),'wb')
 server=subprocess.Popen(['dotnet','run','--project',PROJECT,'--no-build','--no-restore','--launch-profile','https'],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
 log.close()
 for _ in range(60):
  if server.poll() is not None:raise RuntimeError('Server exited before HTTPS readiness; log outside Git')
  try:
   with api.open(BASE+'/api/system/status',timeout=3) as r:
    if r.status==200:break
  except (urllib.error.URLError,TimeoutError):time.sleep(1)
 else:raise RuntimeError('HTTPS readiness not confirmed')
 expect('Exactly one backend',backend_count(),1)
 ownership=json.loads(ps("$p=Get-CimInstance Win32_Process | Where-Object Name -eq 'DuyTien.AssetManagement.Server.exe';$o=Invoke-CimMethod -InputObject $p -MethodName GetOwner;[pscustomobject]@{pid=$p.ProcessId;parent=$p.ParentProcessId;owner=($o.Domain+'\\'+$o.User);current=[Security.Principal.WindowsIdentity]::GetCurrent().Name}|ConvertTo-Json -Compress").decode('utf-8-sig'))
 expect('Backend Windows owner matches operator',ownership['owner'],ownership['current']);state['backendPid']=ownership['pid'];state['launcherPid']=server.pid;save()
 listeners=ps("$listeners=@(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object {$_.LocalPort -in @(7008,5211)});[bool]($listeners.Count -ge 2 -and @($listeners | Where-Object OwningProcess -ne "+str(ownership['pid'])+").Count -eq 0)").decode().strip()
 expect('HTTPS and HTTP listeners owned by this backend',listeners,'True')
 with socket.create_connection(('localhost',7008),timeout=5) as sock:
  with context.wrap_socket(sock,server_hostname='localhost') as tls:
   used=hashlib.sha1(tls.getpeercert(binary_form=True)).hexdigest().upper()
 certs=ps("Get-ChildItem Cert:\\CurrentUser\\My | Where-Object {$_.Subject -eq 'CN=localhost' -and $_.HasPrivateKey} | ForEach-Object {$_.Thumbprint}").decode().split()
 expect('Actual served certificate is current Windows development certificate',used in certs,True)
 state['tlsVerified']=True;save()

def stop_server():
 global server
 if server is None:raise RuntimeError('Refuse to stop a backend not owned by this runner')
 # Only the launcher PID created by this runner and its descendants, not a process-name kill.
 subprocess.run(['taskkill','/PID',str(server.pid),'/T','/F'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,check=True)
 server.wait(timeout=15);server=None
 for _ in range(20):
  if backend_count()==0:return
  time.sleep(.5)
 raise RuntimeError('Backend not stopped; CLI must not write')

def cli(case,args):
 if backend_count():raise RuntimeError('CLI write requires stopped web')
 with open(PRIVATE/('cli-'+uuid.uuid4().hex+'.log'),'wb') as log:r=subprocess.run(['dotnet','run','--project',PROJECT,'--no-build','--no-restore','--launch-profile','https','--',*args],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT)
 expect(case+' exit',r.returncode,0)

def setup():
 if server is None:start_server()
 if 'session' not in state:
  cfg=json.loads(STORE.read_text(encoding='utf-8-sig'));password=cfg.get(KEY)
  if not password:raise RuntimeError('Temporary admin credential absent')
  expect('UAT credential deleted before authenticated checks',secret_delete(),True)
  state['session']=call('Admin application login','POST','/api/auth/login',{'username':'admin','password':password},anonymous=True)
  expect('Existing admin has completed password change',state['session']['mustChangePassword'],False)
  page,_=call('Razor login page','GET','/Account/Login',cookie=True,anonymous=True)
  import re
  match=re.search(rb'name="__RequestVerificationToken"[^>]*value="([^"]+)"',page)
  if not match:raise RuntimeError('Antiforgery token absent')
  raw=urllib.parse.urlencode({'Input.Username':'admin','Input.Password':password,'__RequestVerificationToken':match.group(1).decode()}).encode()
  call('Razor admin login','POST','/Account/Login',raw=raw,mime='application/x-www-form-urlencoded',cookie=True,anonymous=True,expected=302)
  state['cookieToken']=next(c.value for c in jar if c.name=='dt_session');password=None;cfg.clear();save()
 if 'roots' not in state:
  current=call('Read existing DEV tree','GET','/api/categories')
  expect('Preexisting category tree untouched/empty before fake import',len(current['nodes']),0)
  rows=[{'sourceSheet':'F1-F3 Mã ngang','sourceRow':2,'sourceStt':'1','parentSourceStt':None,'level':1,'name':'GIẢ TASK-014 nguồn','segmentCode':'WFA','definition':'FAKE_TASK014_V1 - Fixture hoàn toàn giả'}, {'sourceSheet':'F1-F3 Mã ngang','sourceRow':3,'sourceStt':'1.1','parentSourceStt':'1','level':2,'name':'GIẢ TASK-014 nhánh con','segmentCode':'CHD','definition':'FAKE_TASK014_V1 - Fixture hoàn toàn giả'}, {'sourceSheet':'F1-F3 Mã ngang','sourceRow':4,'sourceStt':'2','parentSourceStt':None,'level':1,'name':'GIẢ TASK-014 đích','segmentCode':'WFB','definition':'FAKE_TASK014_V1 - Fixture hoàn toàn giả'}]
  doc=[{'source_sheet':r['sourceSheet'],'source_row':r['sourceRow'],'source_stt':r['sourceStt'],'source_parent_stt':r['parentSourceStt'],'level':r['level'],'name':r['name'],'segment_code':r['segmentCode'],'definition':r['definition']} for r in rows]
  body,mime=multipart({},'task014-fake.json',json.dumps(doc,ensure_ascii=False).encode(),'application/json')
  p=call('Preview fake DRAFT tree','POST','/api/categories/imports/preview',raw=body,mime=mime)
  expect('Category preview valid',p['preview']['canCommit'],True);expect('Fixture category count',p['preview']['total'],3)
  req={'previewId':p['previewId'],'confirmDuplicateNames':True}
  result=call('Commit fake DRAFT tree','POST','/api/categories/imports/commit',req)
  repeat=call('Replay same category import','POST','/api/categories/imports/commit',req)
  expect('Category replay same tree ID',repeat['treeVersionId'],result['treeVersionId'])
  nodes=call('Read committed fake categories','GET','/api/categories')['nodes']
  state['roots']={n['segmentCode']:n['categoryId'] for n in nodes};state['treeVersionId']=result['treeVersionId'];save()
 if not state.get('referencesSeeded'):
  stop_server();cli('Seed labeled fake reference data',['google-web-dev-demo-references',state['roots']['WFA'],state['roots']['WFB']]);cli('Demo reference seed idempotent repeat',['google-web-dev-demo-references',state['roots']['WFA'],state['roots']['WFB']]);state['referencesSeeded']=True;save();start_server()
 call('Admin login/session survives restart','GET','/api/me')
 state['setupComplete']=True;save()

def files():
 def chunk(t,b):return struct.pack('>I',len(b))+t+b+struct.pack('>I',zlib.crc32(t+b)&0xffffffff)
 png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',1,1,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(b'\0\xff\0\0\xff'))+chunk(b'IEND',b'')
 out=io.BytesIO()
 with zipfile.ZipFile(out,'w',zipfile.ZIP_STORED) as z:
  z.writestr('[Content_Types].xml','<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>')
  z.writestr('_rels/.rels','<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>')
  z.writestr('word/document.xml','<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>FAKE TASK-014</w:t></w:r></w:p></w:body></w:document>')
 docx=out.getvalue()
 # Minimal real PDF structure with page, xref and explicit FAKE text, no company document.
 objects=[b'<</Type /Catalog /Pages 2 0 R>>',b'<</Type /Pages /Kids [3 0 R] /Count 1>>',b'<</Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources <</Font <</F1 4 0 R>>>> /Contents 5 0 R>>',b'<</Type /Font /Subtype /Type1 /BaseFont /Helvetica>>']
 content=b'BT /F1 12 Tf 20 100 Td (FAKE TASK-014) Tj ET';objects.append(b'<</Length '+str(len(content)).encode()+b'>>\nstream\n'+content+b'\nendstream')
 pdf=bytearray(b'%PDF-1.4\n');offsets=[0]
 for i,obj in enumerate(objects,1):offsets.append(len(pdf));pdf.extend(str(i).encode()+b' 0 obj\n'+obj+b'\nendobj\n')
 xref=len(pdf);pdf.extend(b'xref\n0 6\n0000000000 65535 f \n')
 for offset in offsets[1:]:pdf.extend(f'{offset:010d} 00000 n \n'.encode())
 pdf.extend(b'trailer\n<</Size 6 /Root 1 0 R>>\nstartxref\n'+str(xref).encode()+b'\n%%EOF\n')
 return png,docx,bytes(pdf)

def modules():
 call('Razor session before module operations','GET','/Assets',cookie=True)
 root=state['roots']['WFA'];leaf=state['roots']['CHD'];target=state['roots']['WFB']
 choices=call('Asset scope/reference choices','GET','/api/assets/form-choices');expect('Choices DRAFT_DEMO',choices['dataState'],'DRAFT_DEMO')
 asset_input={'name':'GIẢ TASK-014 máy demo','categoryId':leaf,'orgUnitId':'W14-ORG-A','locationId':'W14-LOC-A','type':'MACHINE','notes':'Fixture hoàn toàn giả'}
 req={'asset':asset_input,'expectedVersion':0,'treeVersionId':state['treeVersionId']}
 result=once('asset',lambda:call('Create demo asset in Google','POST','/api/assets',req,key='asset-create',expected=201))
 asset=result['asset'];replay=call('Replay same asset key','POST','/api/assets',req,key='asset-create',expected=201)
 expect('Asset replay same identity',replay['asset']['assetId'],asset['assetId']);expect('Asset explicit demo state',asset['dataState'],'DRAFT_DEMO')
 aid=asset['assetId']
 edit={'asset':dict(asset_input,name='GIẢ TASK-014 máy đã sửa'),'expectedVersion':1,'treeVersionId':state['treeVersionId']}
 edited=once('asset-edit',lambda:call('Edit demo asset','PATCH','/api/assets/'+aid,edit,key='asset-edit'))['asset']
 call('Stale asset version conflict','PATCH','/api/assets/'+aid,edit,key='asset-stale',expected=409)
 expect('Asset identity/code preserved on edit',edited['objectCode'],asset['objectCode'])
 listing=call('Scoped asset search/page','GET','/api/assets?q=TASK-014&page=1&pageSize=1');expect('Scoped asset result',listing['total'],1)
 png,docx,pdf=files()
 def upload(case,path,kind,content,metadata,key):
  filename='task014-fake.'+kind.lower();m={'PHOTO':'image/png','DOCX':'application/vnd.openxmlformats-officedocument.wordprocessingml.document','PDF':'application/pdf'}[kind]
  if kind=='PHOTO':filename='task014-fake.png'
  body,mime=multipart(metadata,filename,content,m);return call(case,'POST',path,key=key,raw=body,mime=mime)
 photo=once('photo',lambda:upload('Upload fake PNG via backend','/api/assets/'+aid+'/photos','PHOTO',png,{'expectedVersion':edited['version'],'description':'GIẢ TASK-014','isPrimary':'true'},'photo'))
 expect('Photo AVAILABLE',photo['file']['state'],'AVAILABLE');photo_repeat=upload('Photo retry same key','/api/assets/'+aid+'/photos','PHOTO',png,{'expectedVersion':edited['version'],'description':'GIẢ TASK-014','isPrimary':'true'},'photo');expect('Photo retry same file',photo_repeat['file']['fileId'],photo['file']['fileId'])
 content,headers=call('Photo gateway download','GET','/api/files/'+photo['file']['fileId']+'/content');expect('Photo actual bytes preserved',content==png,True)
 doc=once('document',lambda:call('Create fake document registry','POST','/api/documents',{'document':{'name':'GIẢ TASK-014 hồ sơ','businessCode':'W14-'+state['runId'],'categoryId':leaf,'description':'Fixture hoàn toàn giả'},'expectedVersion':0},key='document'))
 did=doc['entityId'];ver=once('doc-version',lambda:call('Create fake document version','POST','/api/documents/'+did+'/versions',{'label':'GIẢ V1','expectedDocumentVersion':1},key='doc-version'));vid=ver['entityId']
 dx=once('docx',lambda:upload('Upload fake DOCX','/api/document-versions/'+vid+'/files/DOCX','DOCX',docx,{'expectedVersion':1},'docx'))
 pv=call('Read current version for PDF upload','GET','/api/documents/'+did)['versions'][0]
 pf=once('pdf',lambda:upload('Upload fake PDF','/api/document-versions/'+vid+'/files/PDF','PDF',pdf,{'expectedVersion':pv['version']},'pdf'))
 current=call('Read registry before demo publish','GET','/api/documents/'+did)
 publish={'expectedVersion':current['versions'][0]['version'],'expectedDocumentVersion':current['version'],'effectiveOn':dt.datetime.now(dt.timezone.utc).date().isoformat(),'reason':'GIẢ TASK-014 demo, không tài liệu công ty','confirmDemo':True}
 once('publish',lambda:call('Publish explicitly PUBLISHED_DEMO','POST','/api/document-versions/'+vid+'/publish',publish,key='publish'))
 content,headers=call('Published PDF gateway','GET','/api/files/'+pf['file']['fileId']+'/content');expect('PDF actual bytes preserved',content==pdf,True)
 headers20=['Mã','Tên','Tính chất','Nhóm VTHH','Mô tả','ĐVT chính','Mã nhà SX','Kho ngầm định','Đơn vị chuyển đổi','Tỷ lệ','Phép tính','Mô tả','Giá bán 1','Giá bán 2','Giá bán 3','Giá cố định','Mã NVL','Tên NVL','ĐVT','Số lượng']
 rows=[]
 for i,unit in enumerate(['cái','cái','kg'],1):
  cells=['']*20;cells[0]='W14-'+state['runId']+'-'+str(i);cells[1]='GIẢ TASK-014 Item '+str(i);cells[2]='Vật tư hàng hóa';cells[3]='GIẢ TASK014';cells[4]='Mô tả giả 1';cells[5]=unit;cells[11]='Mô tả giả 2';rows.append({'sourceRow':i+2,'cells':cells})
 imp={'document':{'namespace':'MISA_VTHH','headers':headers20,'rows':rows},'groups':[{'sourceGroup':'GIẢ TASK014','categoryId':leaf,'stockTracked':True}],'decisions':[],'reason':'GIẢ TASK-014, không workbook công ty'}
 preview=call('Preview fake Items','POST','/api/item-imports/preview',imp);expect('Item validation errors',len(preview['errors']),0)
 start={'request':imp,'revision':preview['revision'],'previewHash':preview['previewHash'],'confirm':True}
 job=once('item-job',lambda:call('Start durable fake Item import','POST','/api/item-imports',start,key='item-start'))
 done=once('item-chunk',lambda:call('Commit fake Item chunk','POST','/api/item-imports/'+job['jobId']+'/chunks',{'expectedProgress':0},key='item-chunk'))
 expect('Item job COMPLETE',done['state'],'COMPLETE');again=call('Retry Item chunk same key','POST','/api/item-imports/'+job['jobId']+'/chunks',{'expectedProgress':0},key='item-chunk');expect('No duplicate Item progress',again['processed'],3)
 items=call('Read imported fake Items','GET','/api/items?q=TASK-014')['items'];expect('Assets distinct from three Items',len(items),3)
 for i,item in enumerate(sorted(items,key=lambda i:i['sourceCode']),1):
  request={'itemId':item['itemId'],'locationId':'W14-LOC-A','unit':item['primaryUnit'],'quantity':0 if i==1 else 2.5 if i==2 else None,'minimum':3 if i!=3 else None,'asOf':state['asOf'],'source':'GIẢ TASK-014 explicit snapshot','expectedVersion':0,'reason':'Fixture giả, không suy diễn BOM/kho'}
  bal=once('stock-'+str(i),lambda req=request,i=i:call('Write explicit stock '+str(i),'POST','/api/stock-balances',req,key='stock-'+str(i)))
  expect('Snapshot zero/low/unknown '+str(i),bal['stockStatus'],['OUT','LOW','UNKNOWN'][i-1])
 for route,count in [('assets',1),('assets-by-category',3),('stock-empty',1),('stock-low',1)]:
  report=call('Live report '+route,'GET','/api/reports/'+route+'?Section=MATCHES')
  if route=='assets':expect('Report counts Assets separately',report['summary']['assetCount'],1)
  if route in ('stock-empty','stock-low'):expect('Report matching count '+route,report['total'],count)
 export,headers=call('Scoped Unicode CSV export','GET','/api/reports/stock-empty/export?Section=MATCHES');expect('CSV UTF8BOM',export.startswith(b'\xef\xbb\xbf'),True)
 finish()

def finish():
 call('Razor rendered with valid bearer identity before remaining checks','GET','/Assets',html=True)
 root=state['roots']['WFA'];leaf=state['roots']['CHD'];target=state['roots']['WFB']
 asset=state['steps']['asset']['asset'];aid=asset['assetId'];photo=state['steps']['photo'];doc=state['steps']['document'];did=doc['entityId'];png,docx,pdf=files()
 unknown=call('Explicit UNKNOWN report section on Google','GET','/api/reports/stock-empty?Section=UNKNOWN')
 expect('UNKNOWN is distinct from zero match count',unknown['total'],1)
 expect('UNKNOWN report keeps nullable quantity',unknown['stock'][0]['quantity'],None)
 expect('UNKNOWN unit retained',unknown['stock'][0]['unit'],'kg')
 request={'kind':'ARCHIVE_CATEGORY','reason':'GIẢ TASK-014 relocate registry','categoryId':leaf,'destinationCategoryId':target,'deletePlan':'RELOCATE'}
 preview=once('tree-preview',lambda:call('Full registry tree preview on Google','POST','/api/tree-changes/preview',request));expect('Full registry preview can commit',preview['canCommit'],True)
 kinds={r['kind'] for r in preview['registry']};expect('Preview includes Item/stock/document/file owners',{'ITEM','STOCK','DOCUMENT','PHOTO','DOCX','PDF'}.issubset(kinds),True)
 commit={'request':request,'expectedTreeRevision':preview['treeRevision'],'expectedAssetRevision':preview['assetRevision'],'previewHash':preview['previewHash'],'confirmImpact':True}
 mutation=once('tree-commit',lambda:call('Atomic relocate all four registries','POST','/api/tree-changes/commit',commit,key='tree-relocate'))
 expect('Relocated Item count',mutation['changedItemCount'],3);expect('Relocated Document count',mutation['changedDocumentCount'],1)
 if not state.get('treeReplayVerified'):
  repeat=call('Retry tree commit same key','POST','/api/tree-changes/commit',commit,key='tree-relocate');expect('Tree replay same tree ID',repeat['treeVersionId'],mutation['treeVersionId']);state['treeReplayVerified']=True;save()
 after=call('Asset after registry relocation','GET','/api/assets/'+aid)['asset'];expect('Relocation keeps ObjectCode',after['objectCode'],asset['objectCode']);expect('Asset moved to fake target',after['categoryId'],target)
 after_items=call('Items after registry relocation','GET','/api/items?q=TASK-014')['items'];expect('All Item ownership moved',all(i['categoryId']==target for i in after_items),True)
 after_doc=call('Document after registry relocation','GET','/api/documents/'+did);expect('Document ownership moved',after_doc['categoryId'],target);expect('Published demo version preserved',after_doc['versions'][0]['state'],'PUBLISHED_DEMO')
 content,_=call('Photo gateway after owner scope move','GET','/api/files/'+photo['file']['fileId']+'/content');expect('Photo bytes survive tree mutation',content==png,True)
 stop_server();start_server();call('Login survives module restart','GET','/api/me');persisted=call('Data persisted after restart','GET','/api/assets/'+aid)['asset'];expect('Persisted version after restart',persisted['version'],after['version'])
 # A new labeled Viewer verifies cross-module scope on Google without changing existing users.
 viewer=once('viewer-user',lambda:call('Create fake scoped Viewer fixture','POST','/api/admin/users',{'username':'w14_view_'+state['runId'],'displayName':'GIẢ TASK-014 Viewer'},expected=201))
 assignment={'assignments':[{'roleCode':'Viewer','effectiveFrom':state['asOf'],'effectiveTo':None,'positionId':None,'scopes':[{'scopeType':0,'scopeId':'W14-ORG-A','includeDescendants':True},{'scopeType':1,'scopeId':'W14-LOC-A','includeDescendants':True},{'scopeType':2,'scopeId':root,'includeDescendants':True}]}]}
 once('viewer-assignment',lambda:call('Assign Viewer source-only scope','PUT','/api/admin/users/'+viewer['userId']+'/role-assignments',assignment,expected=204)[0].decode())
 if 'viewer-password' not in state['steps']:state['steps']['viewer-password']=''.join(secrets.choice(string.ascii_lowercase) for _ in range(8));save()
 password=state['steps']['viewer-password']
 if 'viewer-changed' not in state['steps']:
  limited=call('Fake Viewer forced-password login','POST','/api/auth/login',{'username':'w14_view_'+state['runId'],'password':viewer['temporaryPassword']},anonymous=True)
  call('Fake Viewer changes password through approved API','POST','/api/auth/change-password',{'currentPassword':viewer['temporaryPassword'],'newPassword':password},auth_token=limited['accessToken'],expected=204)
  state['steps']['viewer-changed']=True;save()
 scoped=call('Fake Viewer login','POST','/api/auth/login',{'username':'w14_view_'+state['runId'],'password':password},anonymous=True)
 for endpoint in ['/api/assets','/api/items','/api/documents']:
  value=call('Viewer cannot see relocated target '+endpoint,'GET',endpoint,auth_token=scoped['accessToken'])
  count=len(value) if isinstance(value,list) else value.get('total',len(value.get('items',[])))
  expect('Viewer hidden count '+endpoint,count,0)
 report=call('Viewer scoped report aggregate','GET','/api/reports/assets',auth_token=scoped['accessToken']);expect('Scope applied before report total',report['summary']['assetCount'],0)
 call('Viewer blocked tree administration','POST','/api/tree-changes/preview',request,auth_token=scoped['accessToken'],expected=403)
 call('Viewer blocked target photo gateway','GET','/api/files/'+photo['file']['fileId']+'/content',auth_token=scoped['accessToken'],expected=404)
 call('Logout only fake Viewer session','POST','/api/auth/logout',auth_token=scoped['accessToken'],expected=204)
 for path in ['/Admin/Users','/Categories','/Assets','/Documents','/Items','/Stock','/Reports']:
  html,_=call('Razor live rendered bearer '+path,'GET',path,html=True);expect('Razor page renders HTML '+path,b'<!DOCTYPE html>' in html,True)
 call('Runner logout own API session','POST','/api/auth/logout',expected=204)
 state['complete']=True;save()

parser=argparse.ArgumentParser();parser.add_argument('--phase',choices=['setup','modules','finish','all'],default='all');parser.add_argument('--resume',action='store_true');args=parser.parse_args()
exitcode=1
try:
 state=load() if args.resume else {'runId':uuid.uuid4().hex[:12],'asOf':dt.datetime.now(dt.timezone.utc).isoformat(),'steps':{}}
 if args.resume and not state.get('treeReplayVerified') and 'tree-commit' in state.get('steps',{}) and EVIDENCE.exists():
  record=json.loads(EVIDENCE.read_text(encoding='utf-8'));runs=[record]+record.get('previousRuns',[])
  if any(a.get('case')=='Tree replay same tree ID' and a.get('pass') and a.get('actual')==state['steps']['tree-commit']['treeVersionId'] for r in runs for a in r.get('assertions',[])):
   state['treeReplayVerified']=True;save()
 if args.resume and backend_count():
  # Resume reuses one backend; this invocation does not stop it unless it owns its launcher.
  pid=state.get('launcherPid');running=ps('Get-Process -Id '+str(pid)+' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id').decode().strip()
  if not running:raise RuntimeError('Cannot verify owned launcher for resume')
  safe=ps("$p=Get-CimInstance Win32_Process | Where-Object Name -eq 'DuyTien.AssetManagement.Server.exe';[bool]($p.ProcessId -eq "+str(state['backendPid'])+" -and $p.ParentProcessId -eq "+str(pid)+" -and $p.ExecutablePath.StartsWith('"+str(ROOT).replace("'","''")+"',[StringComparison]::OrdinalIgnoreCase))").decode().strip()
  if safe!='True':raise RuntimeError('Backend ownership/path changed; refusing PID stop')
  class Existing:
   def __init__(self,pid):self.pid=pid
   def wait(self,timeout):return 0
  server=Existing(pid)
 else:start_server() if args.phase in ('modules','finish') else None
 if args.phase in ('setup','all'):setup()
 if args.phase in ('modules','all'):modules()
 if args.phase=='finish':finish()
 exitcode=0
except Exception as e:
 events.append({'event':'runner_failure','type':type(e).__name__,'message':str(e) if isinstance(e,RuntimeError) else '[details hidden]','source':'AI_RUN_OBSERVED'})
 print(json.dumps(events[-1],ensure_ascii=False),flush=True)
finally:
 removed=secret_delete();print(json.dumps({'credentialUatDeletedVerified':removed}),flush=True)
 if state.get('complete') and STATE_PATH.exists():STATE_PATH.unlink()
 evidence={'task':'TASK-014','date':'2026-10-14','timezone':'Asia/Saigon','source':'AI_RUN_OBSERVED','phase':args.phase,'fixture':'FAKE_TASK014_DRAFT_DEMO','tlsValidationEnabled':True,'credentialUatDeletedVerified':removed,'checkpointEncryptedOutsideGit':STATE_PATH.exists(),'backendPid':state.get('backendPid'),'launcherPid':state.get('launcherPid'),'assertions':results,'events':events,'passed':sum(r['pass'] for r in results),'failed':sum(not r['pass'] for r in results),'exit':exitcode,'complete':state.get('complete',False),'browser':'NOT_RUN','restore':'NOT_RUN'}
 if EVIDENCE.exists():
  prior=json.loads(EVIDENCE.read_text(encoding='utf-8'));evidence['previousRuns']=prior.get('previousRuns',[])+[{k:v for k,v in prior.items() if k!='previousRuns'}]
 EVIDENCE.write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
sys.exit(exitcode)
