export type User={id:string;name:string;role:'Admin'|'Cashier';username?:string;isActive?:boolean};
export type Product={id:string;serialNumber:string;name:string;categoryId:string;price:number;mrp:number;stockQuantity:number;isActive:boolean;version:number};
export type Category={id:string;name:string;isActive:boolean};
export type Settings={name:string;address:string;phone:string;lowStockThreshold:number};
export type Bill={id:string;billNumber:string;billDate:string;subtotal:number;discountPercentage:number;discountAmount:number;finalAmount:number;shopName:string;shopAddress:string;shopPhone:string;items:{id:string;serialNumber:string;productName:string;priceAtSale:number;quantity:number;total:number}[]};
export type Page<T>={items:T[];total:number};
export const money=(n:number)=>new Intl.NumberFormat('en-IN',{style:'currency',currency:'INR',maximumFractionDigits:2}).format(n);
export const date=(s:string)=>new Date(s).toLocaleString('en-IN',{timeZone:'Asia/Kolkata',dateStyle:'medium',timeStyle:'short'});
