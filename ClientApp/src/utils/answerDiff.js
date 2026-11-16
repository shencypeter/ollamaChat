// Code points preserve emoji surrogate pairs. Prefix/suffix trimming makes small
// edits in long answers cheap; the LCS table has a hard allocation/work ceiling.
export function diffAnswers(left = '', right = '', { maxCells = 250000 } = {}) {
  const a = Array.from(left ?? ''), b = Array.from(right ?? '')
  const segments = []
  function append(type, text) {
    if (!text) return
    const last = segments.at(-1)
    if (last?.type === type) last.text += text
    else segments.push({ type, text })
  }
  let start = 0, endA = a.length, endB = b.length
  while (start < endA && start < endB && a[start] === b[start]) start++
  while (endA > start && endB > start && a[endA - 1] === b[endB - 1]) { endA--; endB-- }
  append('equal', a.slice(0, start).join(''))
  const n = endA - start, m = endB - start
  // A caller may lower the work budget, but may never remove the hard limit.
  const budget = Number.isFinite(maxCells) ? Math.max(0, Math.min(250000, maxCells)) : 250000
  const fallback = n > 0 && m > 0 && (n + 1) * (m + 1) > budget
  if (!n || !m || fallback) {
    append('remove', a.slice(start, endA).join(''))
    append('add', b.slice(start, endB).join(''))
  } else {
    const width = m + 1
    const table = new Uint32Array((n + 1) * width)
    for (let i = n - 1; i >= 0; i--) {
      for (let j = m - 1; j >= 0; j--) {
        table[i * width + j] = a[start + i] === b[start + j]
          ? 1 + table[(i + 1) * width + j + 1]
          : Math.max(table[(i + 1) * width + j], table[i * width + j + 1])
      }
    }
    let i = 0, j = 0
    while (i < n || j < m) {
      if (i < n && j < m && a[start + i] === b[start + j]) {
        append('equal', a[start + i]); i++; j++
      } else if (i < n && (j === m || table[(i + 1) * width + j] >= table[i * width + j + 1])) {
        append('remove', a[start + i]); i++
      } else { append('add', b[start + j]); j++ }
    }
  }
  append('equal', a.slice(endA).join(''))
  return { segments, fallback, unchanged: n === 0 && m === 0 }
}
