// Synthetic transport fixture, bound to loopback. Never logs or persists request bodies.
const http = require('node:http')
const studioTimeouts=new Set()
http.createServer(async (req, res) => {
  if (req.url !== '/api/chat') { res.writeHead(404); res.end(); return }
  let raw = ''; for await (const chunk of req) raw += chunk
  const payload = JSON.parse(raw)
  if (raw.includes('STUDIO_TIMEOUT')) {const key=raw.match(/STUDIO_TIMEOUT_ONCE-[a-z0-9-]+/)?.[0] || 'always';if(key==='always'||!studioTimeouts.has(key)){studioTimeouts.add(key);await new Promise(resolve=>setTimeout(resolve,6000))}}
  if (raw.includes('STUDIO_FAIL')) {res.writeHead(503,{'Content-Type':'application/json'});res.end(JSON.stringify({error:'synthetic unavailable'}));return}
  if (raw.includes('FORMATIVE_HTTP_FAIL')) { res.writeHead(503, { 'Content-Type': 'application/json' }); res.end(JSON.stringify({ error: 'synthetic unavailable', thinking: 'private' })); return }
  if (raw.includes('FORMATIVE_DELAY')) await new Promise(resolve => setTimeout(resolve, 1500))
  let content = '合成傳輸測試回覆'
  if (raw.includes('INSPECTION_INVALID')) content = 'invalid assessment JSON'
  else if(payload.format?.properties?.summary){content=JSON.stringify({summary:'合成案例文字回饋，不代表真實模型品質',supportedPoints:[{point:'已區分資料與推論',evidence:'合成案例原文'}],improvements:[{point:'限制結論範圍',evidence:'合成案例原文',suggestion:'請指出仍需補充的資料'}],nextSteps:['再次核對案例原文']})}
  else if (payload.format) {
    const fields = payload.format.properties.evaluations.items.properties
    const evaluations = fields.version.enum.map(version => ({ version, caseEvidenceAvailable: fields.caseEvidenceAvailable.enum[0], supportedClaims: [], partiallySupportedClaims: [], unsupportedClaims: [], unverifiedClaims: [], importantOmissions: [], reasoningQuality: '合成傳輸測試，不代表模型評分', score: 2, justification: '合成傳輸測試，不代表模型評分' }))
    content = JSON.stringify({ evaluations, comparisonFeedback: evaluations.length === 2 ? '合成比較' : '' })
  }
  res.writeHead(200, { 'Content-Type': 'application/json' })
  res.end(JSON.stringify({ message: { role: 'assistant', content, thinking: 'synthetic private reasoning' }, done_reason: 'stop', eval_count: 12 }))
}).listen(11535, '127.0.0.1', () => console.log('Synthetic Ollama fixture listening on loopback:11535'))
