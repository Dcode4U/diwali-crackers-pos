const base=(import.meta.env.VITE_API_URL||'/api').replace(/\/$/,'');
export class ApiError extends Error{constructor(message:string,public status:number){super(message)}}
export async function api<T>(path:string,method='GET',body?:unknown):Promise<T>{
 let res:Response;try{res=await fetch(`${base}${path}`,{method,credentials:'include',headers:{'Content-Type':'application/json','X-POS-Request':'1'},body:body===undefined?undefined:JSON.stringify(body)});}catch{throw new ApiError('Cannot reach the server. Check your connection and retry.',0)}
 if(!res.ok){const data=await res.json().catch(()=>null);if(res.status===401&&path!='/auth/login')window.dispatchEvent(new Event('session-expired'));throw new ApiError(data?.message|| (data?.errors?Object.values(data.errors).flat().join(' '):res.status===401?'Please sign in again.':res.status===403?'You do not have permission for this action.':res.status===429?'Too many requests. Please wait a minute.':'The request could not be completed.'),res.status)}
 return res.status===204?undefined as T:res.json();
}
