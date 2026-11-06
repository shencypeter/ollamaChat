// Display-only: account records keep the full name. For Chinese names, omit
// the leading surname character; preserve single-character and non-Chinese names.
export function givenName(name) {
  const full = (name || '').trim()
  const characters = Array.from(full)
  return characters.length > 1 && /^\p{Script=Han}+$/u.test(full)
    ? characters.slice(1).join('')
    : full
}
