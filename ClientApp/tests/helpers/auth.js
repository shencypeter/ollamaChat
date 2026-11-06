// Respect the production login rate limit during sequential browser regression suites.
export async function retryLogin(ctx,data){
 for(let attempt=0;attempt<8;attempt++){
  const token=(await(await ctx.get('/api/auth/csrf')).json()).token
  const response=await ctx.post('/api/auth/login',{data,headers:{'X-CSRF-TOKEN':token}})
  if(response.status()!==429)return response
  await new Promise(resolve=>setTimeout(resolve,10000))
 }
 throw new Error('Login remained rate limited after bounded retries')
}
