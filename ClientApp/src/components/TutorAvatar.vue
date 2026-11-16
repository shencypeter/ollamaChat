<template>
  <svg class="tutor-avatar" viewBox="0 0 64 64" aria-hidden="true" focusable="false">
    <circle class="avatar-halo" cx="32" cy="32" r="31" />
    <path fill="currentColor" d="M10 57c1-11 8-18 22-18s21 7 22 18c-12 7-32 7-44 0Z" />
    <path class="avatar-paper" d="m23 41 9 8 9-8-5 17h-8Z" />
    <path fill="currentColor" d="M20 23c0-10 5-16 13-16 9 0 14 6 13 16l-3 8c-2 7-6 11-11 11s-10-5-12-12Z" />
    <path class="avatar-paper" d="M22 21c4 1 8-1 11-5 2 4 6 6 10 6v7c0 6-5 11-11 11s-10-6-10-12Z" />
    <path fill="currentColor" d="M24 26h6v5h-6zm11 0h6v5h-6Z" />
    <path class="avatar-line" d="M30 28h5m-7 7c2 2 5 2 8 0" />
    <path class="avatar-book" d="M15 46c5-1 10 0 17 4v11c-7-4-12-5-17-4Zm34 0c-5-1-10 0-17 4v11c7-4 12-5 17-4Z" />
    <path class="avatar-line" d="M32 50v10" />
    <circle class="avatar-badge" cx="51" cy="14" r="11" />
    <path class="avatar-brain" d="M51 8c-3-3-7 0-5 3-4 0-4 5-1 6-1 4 4 6 6 3 2 3 7 1 6-3 3-1 3-6-1-6 2-3-2-6-5-3Zm0 0v12m-4-7c2 0 3 1 3 3m5-3c-2 0-3 1-3 3" />
  </svg>
</template>

<style scoped>
.tutor-avatar{display:block;width:48px;height:48px;flex:none;color:var(--accent);overflow:visible}
.avatar-halo{fill:var(--accent-soft)}.avatar-paper{fill:var(--surface)}
.avatar-line{fill:none;stroke:currentColor;stroke-width:1.5;stroke-linecap:round;stroke-linejoin:round}
.avatar-book{fill:var(--accent-soft);stroke:currentColor;stroke-width:1.3;stroke-linejoin:round}
.avatar-badge{fill:var(--surface);stroke:#438b88;stroke-width:1.5}
.avatar-brain{fill:none;stroke:#438b88;stroke-width:1.35;stroke-linecap:round;stroke-linejoin:round}
@media(max-width:600px){.tutor-avatar{width:42px;height:42px}}
</style>
