// Authentication redirects may return only to a page on this site.
export function safeReturnPath(value) {
  if(typeof value!=='string'||!value.startsWith('/')||value.startsWith('//')||/[\\\u0000-\u001f\u007f]/.test(value))return '/'
  const path=value.split(/[?#]/)[0]
  if(path==='/login'||path.startsWith('/api/')||path==='/api')return '/'
  return value
}
