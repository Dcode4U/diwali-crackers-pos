// DOM-level UI verification with explicit fixtures. This does NOT replace PostgreSQL acceptance tests.
import React from 'react';
import {afterEach,beforeEach,describe,it,expect,vi} from 'vitest';
import {render,screen,waitFor,cleanup,fireEvent} from '@testing-library/react';
import {QueryClient,QueryClientProvider} from '@tanstack/react-query';
import {POS} from '../src/POS';
const products=[{id:'p1',serialNumber:'1001',name:'Flower Pot Small',categoryId:'c1',price:150,mrp:200,stockQuantity:100,isActive:true,version:1},{id:'p2',serialNumber:'1003',name:'Sparklers 10cm',categoryId:'c1',price:80,mrp:100,stockQuantity:200,isActive:true,version:1}];
let submits:Record<string,any>[]=[];let failFirst=false;
beforeEach(()=>{submits=[];failFirst=false;vi.stubGlobal('fetch',vi.fn(async(url:string,init?:RequestInit)=>{
 let data:unknown={};
 if(url.includes('/products/search'))data={items:products,total:2};
 else if(url.endsWith('/categories'))data=[{id:'c1',name:'Crackers',isActive:true}];
 else if(url.endsWith('/settings'))data={name:'Diwali Crackers',lowStockThreshold:10};
 else if(url.endsWith('/bills')){const input=JSON.parse(init?.body as string);submits.push(input);if(failFirst&&submits.length===1)throw new TypeError('Network failed');const items=input.items.map((i:any)=>{const p=products.find(p=>p.id===i.productId)!;return{id:p.id,serialNumber:p.serialNumber,productName:p.name,quantity:i.quantity,priceAtSale:p.price,total:p.price*i.quantity}});const subtotal=items.reduce((s:number,i:any)=>s+i.total,0);data={id:'b1',billNumber:'INV-2026-000001',billDate:'2026-10-01T12:00:00Z',shopName:'Diwali Crackers',shopAddress:'Hyderabad',shopPhone:'',items,subtotal,discountPercentage:input.discountPercentage,discountAmount:subtotal*input.discountPercentage/100,finalAmount:subtotal*(1-input.discountPercentage/100)}}
 return new Response(JSON.stringify(data),{status:200,headers:{'Content-Type':'application/json'}})
}));});
afterEach(()=>{cleanup();vi.unstubAllGlobals()});
function start(){const client=new QueryClient({defaultOptions:{queries:{retry:false},mutations:{retry:false}}});render(<QueryClientProvider client={client}><POS/></QueryClientProvider>)}
async function addFirst(){fireEvent.click(await screen.findByRole('button',{name:/Flower Pot Small/}))}
describe('POS interface',()=>{
 it('calculates 990 subtotal, 99 discount and 891 total, saves and starts a new bill',async()=>{start();await addFirst();fireEvent.click(screen.getByRole('button',{name:/Sparklers 10cm/}));fireEvent.change(screen.getByLabelText('Boxes of Flower Pot Small'),{target:{value:'5'}});fireEvent.change(screen.getByLabelText('Boxes of Sparklers 10cm'),{target:{value:'3'}});fireEvent.change(screen.getByLabelText('Discount'),{target:{value:'10'}});expect(screen.getByText('₹891.00')).toBeTruthy();fireEvent.click(screen.getByRole('button',{name:'Complete bill'}));await screen.findByText('INV-2026-000001');expect(submits).toHaveLength(1);expect(submits[0].items.map((x:any)=>x.quantity)).toEqual([5,3]);fireEvent.click(screen.getByRole('button',{name:'New bill'}));expect(screen.getByText('Your bill starts here')).toBeTruthy();await waitFor(()=>expect(document.activeElement).toBe(screen.getByLabelText('Search product or serial number')))});
 it('blocks overstock quantities and invalid discounts',async()=>{start();await addFirst();fireEvent.change(screen.getByLabelText('Boxes of Flower Pot Small'),{target:{value:'101'}});expect(screen.getByRole('alert').textContent).toContain('1–100');expect((screen.getByLabelText('Boxes of Flower Pot Small') as HTMLInputElement).value).toBe('1');fireEvent.change(screen.getByLabelText('Discount'),{target:{value:'101'}});expect((screen.getByRole('button',{name:'Complete bill'}) as HTMLButtonElement).disabled).toBe(true)});
 it('retries an uncertain sale with the same key and payload',async()=>{failFirst=true;start();await addFirst();fireEvent.click(screen.getByRole('button',{name:'Complete bill'}));const retry=await screen.findByRole('button',{name:'Retry same bill'});expect((screen.getByLabelText('Discount') as HTMLInputElement).disabled).toBe(true);fireEvent.click(retry);await screen.findByText('INV-2026-000001');expect(submits).toHaveLength(2);expect(submits[0]).toEqual(submits[1])});
});
