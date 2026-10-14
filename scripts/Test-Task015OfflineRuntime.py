"""Actual Release/HTTPS verification. Only an explicitly exported synthetic FAKE-TASK015 workspace.
Never accepts a company/Google workspace or application credential. The fake fixture account is test-only.
"""
import argparse,datetime,html,http.cookiejar,json,os,pathlib,ssl,struct,subprocess,time,urllib.request,urllib.error,uuid,zlib
parser=argparse.ArgumentParser();parser.add_argument('--workspace',required=True);parser.add_argument('--output',required=True);args=parser.parse_args()
root=pathlib.Path(__file__).resolve().parents[1];workspace=pathlib.Path(args.workspace).resolve();out=pathlib.Path(args.output).resolve()
if not workspace.name.startswith('FAKE-TASK015-') or json.loads((workspace/'FAKE-TASK015-marker.json').read_text()).get('fixture') is not True:raise RuntimeError('Only exported synthetic fixture allowed')
if workspace.is_relative_to(root):raise RuntimeError('Fixture workspace must be outside Git')
checks=[];metrics=[];report={'source':'ACTUAL_RELEASE_PROCESS_HTTPS_PRODUCTION_LOCAL_ADAPTER_SYNTHETIC','nodes':665,'items':20744,'tlsValidation':True,'browser':'NOT_RUN','checks':checks,'metrics':metrics}
def check(name,ok):
 checks.append({'case':name,'passed':bool(ok)})
 if not ok:raise RuntimeError('Assertion failed: '+name)
def operator(script,*arguments):
 with (workspace/'operator.private.log').open('ab') as log:
  r=subprocess.run(['powershell.exe','-NoProfile','-File',str(root/'scripts'/script),'-Workspace',str(workspace),*arguments],cwd=root,stdin=subprocess.DEVNULL,stdout=log,stderr=log)
 if r.returncode:raise RuntimeError('Operator script failed: '+script+' exit '+str(r.returncode))
 return r.returncode
class NoRedirect(urllib.request.HTTPRedirectHandler):
 def redirect_request(self,req,fp,code,msg,headers,newurl):return None
jar=http.cookiejar.CookieJar();client=urllib.request.build_opener(urllib.request.HTTPSHandler(context=ssl.create_default_context()),urllib.request.HTTPCookieProcessor(jar),NoRedirect())
base='https://localhost:7008';bearer=None

def call(method,path,data=None,key=None,raw=None,mime=None,use_token=False):
 headers={'Accept':'application/json' if path.startswith('/api/') else 'text/html'}
 if use_token:headers['Authorization']='Bearer '+bearer
 if key:headers['Idempotency-Key']=key
 if data is not None:raw=json.dumps(data,ensure_ascii=False).encode();mime='application/json'
 if mime:headers['Content-Type']=mime
 req=urllib.request.Request(base+path,data=raw,headers=headers,method=method)
 try:r=client.open(req,timeout=120)
 except urllib.error.HTTPError as e:r=e
 with r:return r.status,r.read(),r.headers

def asjson(response):return json.loads(response[1])
def measure(name,path,count=21):
 samples=[];cold=None;maxbytes=0
 for n in range(count):
  start=time.perf_counter();status,body,headers=call('GET',path);elapsed=(time.perf_counter()-start)*1000;check(name+' HTTP200 sample'+str(n),status==200);maxbytes=max(maxbytes,len(body))
  if n==0:cold=elapsed
  else:samples.append(elapsed)
 samples.sort();p95=samples[int(__import__('math').ceil(len(samples)*.95))-1];metrics.append({'case':name,'path':path,'coldFirstMs':cold,'warmCount':len(samples),'warmP50Ms':samples[int(__import__('math').ceil(len(samples)*.5))-1],'warmP95Ms':p95,'maxMs':max(samples),'maxBytes':maxbytes,'targetMs':500,'observedTargetMet':p95<500})

def multipart(fields,name,content):
 boundary='fixture-'+uuid.uuid4().hex;data=b''
 for k,v in fields.items():data+=('--'+boundary+'\r\nContent-Disposition: form-data; name="'+k+'"\r\n\r\n'+str(v)+'\r\n').encode()
 data+=('--'+boundary+'\r\nContent-Disposition: form-data; name="file"; filename="'+name+'"\r\nContent-Type: image/png\r\n\r\n').encode()+content+('\r\n--'+boundary+'--\r\n').encode()
 return data,'multipart/form-data; boundary='+boundary
try:
 start=time.perf_counter();operator('Start-LocalOffline.ps1','-NoBrowser');report['launcherStartMs']=(time.perf_counter()-start)*1000
 state=asjson(call('GET','/api/system/status'));check('Explicit LocalOffline mode',state.get('mode')=='LocalOffline');check('Initial outbound attempts zero',state.get('outboundAttempts')==0)
 record=json.loads((workspace/'backend-process.json').read_text(encoding='utf-8-sig'));pid=record['Pid'];report['backendPidBeforeRestart']=pid
 operator('Start-LocalOffline.ps1','-NoBrowser');record2=json.loads((workspace/'backend-process.json').read_text(encoding='utf-8-sig'));check('Repeated launcher reuses same PID',record2['Pid']==pid)
 import re,urllib.parse
 start=time.perf_counter();status,body,_=call('GET','/Account/Login');check('Login Razor accessible strict HTTPS',status==200);report['loginGetColdMs']=(time.perf_counter()-start)*1000
 token=html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"',body.decode()).group(1))
 payload=urllib.parse.urlencode({'__RequestVerificationToken':token,'Input.Username':'admin','Input.Password':'b'*8}).encode()
 start=time.perf_counter();status,_,headers=call('POST','/Account/Login',raw=payload,mime='application/x-www-form-urlencoded');report['loginPostColdMs']=(time.perf_counter()-start)*1000;check('Local fixture Razor login succeeded',status==302);check('Workspace cookie isolated',any(c.name.startswith('dt_local_') and c.secure for c in jar))
 for resource in ['/js/site.js','/lib/bootstrap/dist/css/bootstrap.min.css','/lib/jquery/dist/jquery.min.js']:
  check('Bundled resource '+resource,call('GET',resource)[0]==200)
 for name,path in [('categories','/Categories'),('assets','/Assets'),('items','/Items'),('reportR01','/Reports?Code=R01'),('reportR04','/Reports?Code=R04')]:measure(name,path)
 login=call('POST','/api/auth/login',{'username':'admin','password':'b'*8});check('API fixture login',login[0]==200);bearer=asjson(login)['accessToken']
 choices=asjson(call('GET','/api/assets/form-choices',use_token=True));req={'asset':{'name':'GIẢ TASK-015 actual Release','categoryId':choices['categories'][0]['id'],'orgUnitId':choices['orgUnits'][0]['id'],'locationId':choices['locations'][0]['id'],'type':'MACHINE','condition':'UNKNOWN'},'expectedVersion':0,'treeVersionId':choices['treeVersionId']}
 first=call('POST','/api/assets',req,key='runtime-create',use_token=True);check('Actual durable create HTTP201',first[0]==201);asset=asjson(first)['asset'];again=asjson(call('POST','/api/assets',req,key='runtime-create',use_token=True));check('Same key replays same ID/code',again['replayed'] and again['asset']['assetId']==asset['assetId'] and again['asset']['objectCode']==asset['objectCode'])
 edit={'asset':dict(req['asset'],name='GIẢ TASK-015 actual Release đã sửa'),'expectedVersion':1,'treeVersionId':req['treeVersionId']}
 edited=call('PATCH','/api/assets/'+asset['assetId'],edit,key='runtime-edit',use_token=True);check('Actual durable edit',edited[0]==200 and asjson(edited)['asset']['version']==2);editedAgain=call('PATCH','/api/assets/'+asset['assetId'],edit,key='runtime-edit',use_token=True);check('Edit retry replays',editedAgain[0]==200 and asjson(editedAgain)['replayed'])
 stale=call('PATCH','/api/assets/'+asset['assetId'],req,key='runtime-stale',use_token=True);check('Stale version rejected',stale[0]==409)
 def chunk(kind,data):return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
 png=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',1,1,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(b'\x00\x00\xff\x00\xff'))+chunk(b'IEND',b'')
 raw,mime=multipart({'expectedVersion':1,'description':'GIẢ actual runtime','isPrimary':'true'},'FAKE.png',png);photo=call('POST','/api/assets/'+asset['assetId']+'/photos',key='runtime-photo',raw=raw,mime=mime,use_token=True);check('Photo upload AVAILABLE',photo[0]==200 and asjson(photo)['file']['state']=='AVAILABLE');fileid=asjson(photo)['file']['fileId']
 repeat=call('POST','/api/assets/'+asset['assetId']+'/photos',key='runtime-photo',raw=raw,mime=mime,use_token=True);check('Photo retry no duplicate',repeat[0]==200 and asjson(repeat)['file']['fileId']==fileid);check('Gateway actual bytes',call('GET','/api/files/'+fileid+'/content',use_token=True)[1]==png)
 check('No runtime outbound attempts through login/write/file/report',asjson(call('GET','/api/system/status'))['outboundAttempts']==0)
 operator('Stop-LocalOffline.ps1');operator('Start-LocalOffline.ps1','-NoBrowser');report['backendPidAfterRestart']=json.loads((workspace/'backend-process.json').read_text(encoding='utf-8-sig'))['Pid']
 check('Session and asset survive actual process restart',call('GET','/api/assets/'+asset['assetId'],use_token=True)[0]==200);check('Razor cookie survives restart',call('GET','/Assets')[0]==200);check('Photo bytes survive restart',call('GET','/api/files/'+fileid+'/content',use_token=True)[1]==png)
 check('Still zero outbound attempts',asjson(call('GET','/api/system/status'))['outboundAttempts']==0)
 report['complete']=True
except Exception as e:
 report['complete']=False;report['failureType']=type(e).__name__;report['failureMessage']=str(e) if isinstance(e,RuntimeError) else 'Details kept private; no credential printed';raise
finally:
 try:
  if (workspace/'backend-process.json').exists():operator('Stop-LocalOffline.ps1');report['ownedBackendStopped']=True
 except Exception:report['ownedBackendStopped']=False
 out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps({'complete':report.get('complete'), 'checksPassed':sum(c['passed'] for c in checks),'checksTotal':len(checks),'ownedBackendStopped':report.get('ownedBackendStopped')},ensure_ascii=False))
