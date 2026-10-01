"""Real HTTP/PostgreSQL acceptance suite. Use only an isolated test database."""
import concurrent.futures, http.cookiejar, json, os, time, urllib.request, urllib.error, uuid
BASE=os.environ.get('TEST_API_URL','http://127.0.0.1:5080')
class Client:
 def __init__(self): self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
 def request(self,path,method='GET',body=None,expected=200):
  req=urllib.request.Request(BASE+path,method=method,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json','X-POS-Request':'1'})
  try:
   with self.opener.open(req,timeout=30) as r: code=r.status;raw=r.read()
  except urllib.error.HTTPError as e: code=e.code;raw=e.read()
  data=json.loads(raw) if raw else None
  assert code==expected, f'{method} {path}: expected {expected}, got {code}: {data}'
  return data
 def login(self,name,password): return self.request('/api/auth/login','POST',{'username':name,'password':password})
for attempt in range(60):
 try:
  with urllib.request.urlopen(BASE+'/health',timeout=2) as r:
   if r.status==200:break
 except Exception:time.sleep(1)
else:raise RuntimeError('API did not become healthy')
admin=Client();admin.login(os.environ['SeedAdmin__Username'],os.environ['SeedAdmin__Password'])
suffix=uuid.uuid4().hex[:10]
category=admin.request('/api/categories','POST',{'name':'Acceptance '+suffix,'isActive':True})
def product(serial,stock=10,price=150):
 return admin.request('/api/products','POST',{'serialNumber':suffix+serial,'name':'Test '+serial,'categoryId':category['id'],'price':price,'mrp':300,'stockQuantity':stock,'isActive':True})
def sale(p,quantity=1,key=None,discount=0):
 return {'submissionId':key or str(uuid.uuid4()),'discountPercentage':discount,'items':[{'productId':p['id'],'quantity':quantity,'expectedPrice':p['price']}]}
def get(p):return admin.request('/api/products/'+p['id'])
# Role boundaries and ownership.
cashiers=[]
for i in range(2):
 username='cashier'+suffix+str(i);password=uuid.uuid4().hex
 admin.request('/api/users','POST',{'name':'Test cashier','username':username,'password':password,'role':'Cashier','isActive':True})
 c=Client();c.login(username,password);cashiers.append(c)
cashiers[0].request('/api/users',expected=403)
cashiers[0].request('/api/inventory/adjust','POST',{'productId':str(uuid.uuid4()),'quantity':1,'reason':'test'},expected=403)
p=product('base');p2=product('spark',stock=10,price=80)
assert admin.request('/api/products/search?q='+p['serialNumber'])['items'][0]['id']==p['id']
payload=sale(p,5,discount=10);payload['items'].append({'productId':p2['id'],'quantity':3,'expectedPrice':80})
bill=cashiers[0].request('/api/bills','POST',payload)
assert (bill['subtotal'],bill['discountAmount'],bill['finalAmount'])==(990,99,891)
assert get(p)['stockQuantity']==5 and get(p2)['stockQuantity']==7
# Duplicate retry returns exactly the original committed bill and does not deduct twice.
retry=cashiers[0].request('/api/bills','POST',payload);assert retry['id']==bill['id'];assert get(p)['stockQuantity']==5
altered={**payload,'discountPercentage':20};cashiers[0].request('/api/bills','POST',altered,expected=409)
cashiers[1].request('/api/bills/'+bill['id'],expected=404)
# Historical price snapshot survives a current-price update.
updated=get(p);updated['price']=180
admin.request('/api/products/'+p['id'],'PUT',updated)
historical=cashiers[0].request('/api/bills/'+bill['id']);assert next(x for x in historical['items'] if x['productId']==p['id'])['priceAtSale']==150
cashiers[0].request('/api/bills','POST',sale(p),expected=409)
# Whole transaction rolls back if ANY line cannot be fulfilled.
a=product('rollbackA',stock=4);b=product('rollbackB',stock=0)
failed=sale(a);failed['items'].append({'productId':b['id'],'quantity':1,'expectedPrice':150})
cashiers[0].request('/api/bills','POST',failed,expected=409);assert get(a)['stockQuantity']==4
# Two separate cashiers racing for the final box: one and only one succeeds.
last=product('last',stock=1)
def race(i):
 try:return cashiers[i].request('/api/bills','POST',sale(last))
 except AssertionError as e:
  assert 'got 409' in str(e);return None
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:results=list(pool.map(race,[0,1]))
assert sum(x is not None for x in results)==1 and get(last)['stockQuantity']==0
# Concurrent same-key requests serialize and share one bill.
dup=product('duplicate',stock=3);same=sale(dup);sessions=[]
for _ in range(2):
 c=Client();c.login(os.environ['SeedAdmin__Username'],os.environ['SeedAdmin__Password']);sessions.append(c)
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:results=list(pool.map(lambda c:c.request('/api/bills','POST',same),sessions))
assert results[0]['id']==results[1]['id'] and get(dup)['stockQuantity']==2
# Validation and atomic adjustments.
admin.request('/api/products','POST',{'serialNumber':p['serialNumber'],'name':'Duplicate','categoryId':category['id'],'price':10,'mrp':20,'stockQuantity':0,'isActive':True},expected=409)
admin.request('/api/bills','POST',{'submissionId':str(uuid.uuid4()),'discountPercentage':0,'items':[]},expected=400)
admin.request('/api/bills','POST',sale(dup,discount=101),expected=400)
admin.request('/api/bills','POST',sale(dup,quantity=0),expected=400)
admin.request('/api/inventory/adjust','POST',{'productId':dup['id'],'quantity':-3,'reason':'Would go negative'},expected=400)
assert get(dup)['stockQuantity']==2
admin.request('/api/inventory/adjust','POST',{'productId':dup['id'],'quantity':5,'reason':'Received stock'})
assert get(dup)['stockQuantity']==7
cashiers[0].request('/api/auth/logout','POST',expected=204)
cashiers[0].request('/api/products',expected=401)
print('PASS: billing, calculations, stock, rollback, historical prices, idempotency, concurrency, roles, ownership, validation, logout')
