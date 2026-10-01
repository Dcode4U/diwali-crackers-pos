import {type ReactNode} from 'react';
import {X,LoaderCircle} from 'lucide-react';
export function ErrorBox({error}:{error:unknown}){return error?<div role="alert" className="error">{error instanceof Error?error.message:String(error)}</div>:null}
export function Loading(){return <div className="loading"><LoaderCircle className="spin" size={20}/> Loading…</div>}
export function Empty({children}:{children:ReactNode}){return <div className="empty">{children}</div>}
export function Modal({title,children,onClose}:{title:string;children:ReactNode;onClose:()=>void}){return <div className="overlay" onMouseDown={e=>{if(e.target===e.currentTarget)onClose()}}><section className="modal" role="dialog" aria-modal="true" aria-label={title}><header><h2>{title}</h2><button className="icon" aria-label="Close" onClick={onClose}><X size={20}/></button></header>{children}</section></div>}
export function Field({label,children}:{label:string;children:ReactNode}){return <label className="field"><span>{label}</span>{children}</label>}
export function Pager({page,total,size=50,setPage}:{page:number;total:number;size?:number;setPage:(p:number)=>void}){return <div className="pager"><span>{total} records · Page {page}</span><button disabled={page===1} onClick={()=>setPage(page-1)}>Previous</button><button disabled={page*size>=total} onClick={()=>setPage(page+1)}>Next</button></div>}
