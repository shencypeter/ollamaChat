<script setup>
import {computed,nextTick,onMounted,ref} from 'vue'
import {ChevronLeft,ChevronRight,Lock,LockOpen,MessageCircle,MessagesSquare,Paperclip,Plus,RefreshCw,ThumbsUp,X} from '@lucide/vue'
import {api} from '../api'
import {markNotificationsRead} from '../composables/notifications'
import {newRequestId} from '../utils/requestId'

const props=defineProps({course:Object})
const base=`/courses/${props.course.id}/questions`
const data=ref(null),selectedId=ref(null),replies=ref([]),error=ref(''),notice=ref(''),busy=ref(false)
const creating=ref(false),title=ref(''),question=ref(''),replyText=ref(''),file=ref(null),picker=ref(null)
let requestId=newRequestId(),sentText=null,replyRequestId=newRequestId(),sentReply=null

const selected=computed(()=>data.value?.items.find(item=>item.id===selectedId.value)||null)
const selectedIndex=computed(()=>data.value?.items.findIndex(item=>item.id===selectedId.value)??-1)
const previous=computed(()=>selectedIndex.value>0?data.value.items[selectedIndex.value-1]:null)
const next=computed(()=>selectedIndex.value>=0&&selectedIndex.value<data.value.items.length-1?data.value.items[selectedIndex.value+1]:null)
const roleLabel=role=>role==='teacher'?'教授':role==='ta'?'助教':null
const format=value=>new Date(value).toLocaleString()

async function run(action){busy.value=true;error.value='';notice.value='';try{await action()}catch(e){error.value=e.message}finally{busy.value=false}}
async function load(keepSelection=true){
  const result=await api(`${base}?all=true`);data.value=result
  if(!keepSelection||!result.items.some(item=>item.id===selectedId.value))selectedId.value=result.items[0]?.id||null
  if(result.canModerate){await nextTick();await markNotificationsRead('question',result.items.map(item=>item.id))}
  await loadReplies()
}
async function loadReplies(){
  if(!selectedId.value){replies.value=[];return}
  const result=await api(`${base}/${selectedId.value}/replies?all=true`);replies.value=result.items
}
async function selectThread(item){selectedId.value=item.id;replyText.value='';sentReply=null;replyRequestId=newRequestId();await run(loadReplies)}
async function ask(){await run(async()=>{
  const signature=`${title.value}\n${question.value}`
  if(sentText!==signature){requestId=newRequestId();sentText=signature}
  if(file.value){const body=new FormData();body.append('id',requestId);body.append('title',title.value);body.append('text',question.value);body.append('file',file.value);await api(base+'/with-pdf',{method:'POST',body});file.value=null;if(picker.value)picker.value.value=''}
  else await api(base,{method:'POST',body:{id:requestId,title:title.value,text:question.value}})
  title.value='';question.value='';sentText=null;requestId=newRequestId();creating.value=false
  await load(false);notice.value='討論串已發布，同課成員現在即可參與。'
})}
async function participate(){await run(async()=>{
  if(sentReply!==replyText.value){replyRequestId=newRequestId();sentReply=replyText.value}
  await api(`${base}/${selectedId.value}/replies`,{method:'POST',body:{id:replyRequestId,text:replyText.value}})
  replyText.value='';sentReply=null;replyRequestId=newRequestId();await load(true);notice.value='回覆已送出。'
})}
async function likeThread(){await run(async()=>{await api(`${base}/${selected.value.id}/vote`,{method:'PUT',body:!selected.value.liked});await load(true)})}
async function likeReply(item){await run(async()=>{await api(`${base}/${selected.value.id}/replies/${item.id}/like`,{method:'PUT',body:{liked:!item.liked}});await loadReplies()})}
async function toggleClosed(){await run(async()=>{await api(`${base}/${selected.value.id}/lock`,{method:'PUT',body:!selected.value.locked});await load(true);notice.value=selected.value.locked?'討論串已關閉，仍可閱讀與按讚。':'討論串已重新開放。'})}
onMounted(()=>run(()=>load(false)))
</script>

<template>
  <section class="surface discussion-panel">
    <header class="discussion-heading">
      <div><h2><MessagesSquare :size="22"/> 課程討論</h2><p class="muted">全課公開討論；學生、教師與助教都能發起及回覆。</p></div>
      <button class="button secondary" type="button" :disabled="busy" @click="run(()=>load(true))"><RefreshCw :size="15"/>重新整理</button>
    </header>
    <p v-if="error" class="error-box" role="alert">{{error}}</p>
    <p v-if="notice" class="discussion-notice" role="status">{{notice}}</p>

    <div v-if="data" class="discussion-layout">
      <aside class="thread-sidebar" aria-label="所有討論串">
        <div class="thread-sidebar-heading">
          <div><strong>所有討論串</strong><span>{{data.total}} 篇</span></div>
          <button v-if="data.canAsk" class="button primary new-thread-button" type="button" :aria-expanded="creating" @click="creating=!creating"><X v-if="creating" :size="16"/><Plus v-else :size="16"/>{{creating?'取消':'新增討論'}}</button>
        </div>
        <form v-if="creating" class="new-thread-form" @submit.prevent="ask">
          <label>主題<input v-model="title" required maxlength="200" placeholder="清楚描述討論主題"/></label>
          <label>內容<textarea v-model="question" required maxlength="8000" rows="5" placeholder="補充背景、想法或問題"></textarea></label>
          <label class="file-label"><Paperclip :size="14"/>公開 PDF（選填，5 MB）<input ref="picker" type="file" accept="application/pdf,.pdf" @change="file=$event.target.files[0];sentText=null"/></label>
          <button class="button primary" :disabled="busy||!title.trim()||!question.trim()">發布討論</button>
        </form>
        <div v-if="data.items.length" class="thread-list">
          <button v-for="item in data.items" :key="item.id" type="button" class="thread-row" :class="{active:item.id===selectedId}" :aria-current="item.id===selectedId?'true':undefined" @click="selectThread(item)">
            <span class="thread-row-title">{{item.title}}</span>
            <span class="thread-row-meta"><span>{{item.author}}</span><span><MessageCircle :size="12"/>{{item.replyCount}}</span><span><ThumbsUp :size="12"/>{{item.likes}}</span></span>
            <span v-if="item.locked" class="closed-label"><Lock :size="11"/>已關閉</span>
          </button>
        </div>
        <p v-else class="empty-thread-list">目前還沒有討論串。</p>
      </aside>

      <main v-if="selected" class="thread-reader" aria-live="polite">
        <nav class="thread-navigation" aria-label="討論串前後篇">
          <button class="button secondary" type="button" :disabled="busy||!previous" @click="selectThread(previous)"><ChevronLeft :size="16"/>前一篇</button>
          <span>{{selectedIndex+1}} / {{data.items.length}}</span>
          <button class="button secondary" type="button" :disabled="busy||!next" @click="selectThread(next)">後一篇<ChevronRight :size="16"/></button>
        </nav>

        <article class="thread-original">
          <div class="thread-title-line"><div><p class="eyebrow">討論主題</p><h2>{{selected.title}}</h2></div><span v-if="selected.locked" class="closed-badge"><Lock :size="13"/>討論已關閉</span></div>
          <div class="author-line"><strong>{{selected.author}}</strong><span v-if="roleLabel(selected.authorRole)" class="moderator-badge">{{roleLabel(selected.authorRole)}} · Moderator</span><time>{{format(selected.createdAt)}}</time></div>
          <p class="interaction-text thread-body">{{selected.question}}</p>
          <a v-if="selected.attachmentName" class="attachment-link" :href="`/api${base}/${selected.id}/attachment`" download><Paperclip :size="15"/>{{selected.attachmentName}}</a>
          <div class="thread-actions"><button class="button secondary like-button" type="button" :disabled="busy||!data.canAsk" :aria-pressed="selected.liked" @click="likeThread"><ThumbsUp :size="15"/>{{selected.liked?'已讚':'讚'}} {{selected.likes}}</button><button v-if="data.canModerate" class="button secondary moderator-action" type="button" :disabled="busy" @click="toggleClosed"><LockOpen v-if="selected.locked" :size="15"/><Lock v-else :size="15"/>{{selected.locked?'重新開放':'關閉討論'}}</button></div>
        </article>

        <section class="reply-list" aria-label="討論回覆">
          <h3>{{selected.replyCount}} 則回覆</h3>
          <article v-if="selected.answeredAt" class="discussion-reply moderator-reply">
            <div class="author-line"><strong>{{selected.answeredBy}}</strong><span class="moderator-badge">{{roleLabel(selected.answeredByRole)||'課程團隊'}} · Moderator</span><time>{{format(selected.answeredAt)}}</time></div>
            <p class="interaction-text">{{selected.response}}</p>
          </article>
          <article v-for="item in replies" :key="item.id" class="discussion-reply" :class="{'moderator-reply':roleLabel(item.authorRole)}">
            <div class="author-line"><strong>{{item.author}}</strong><span v-if="roleLabel(item.authorRole)" class="moderator-badge">{{roleLabel(item.authorRole)}} · Moderator</span><time>{{format(item.createdAt)}}</time></div>
            <p class="interaction-text">{{item.body}}</p>
            <button class="reply-like" type="button" :disabled="busy||!data.canAsk" :aria-pressed="item.liked" @click="likeReply(item)"><ThumbsUp :size="14"/>{{item.liked?'已讚':'讚'}} {{item.likes}}</button>
          </article>
          <p v-if="!selected.replyCount" class="empty-replies">還沒有回覆，成為第一位參與討論的人。</p>
        </section>

        <form v-if="data.canReply&&!selected.locked" class="reply-form" @submit.prevent="participate">
          <label>加入討論<textarea v-model="replyText" maxlength="8000" required rows="4" placeholder="回覆此討論串"></textarea></label>
          <button class="button primary" :disabled="busy||!replyText.trim()">送出回覆</button>
        </form>
        <p v-else-if="selected.locked" class="closed-explanation"><Lock :size="15"/>Moderator 已關閉此討論串，內容仍可閱讀與按讚。</p>

        <nav class="thread-navigation bottom-navigation" aria-label="討論串前後篇">
          <button class="button secondary" type="button" :disabled="busy||!previous" @click="selectThread(previous)"><ChevronLeft :size="16"/>前一篇</button>
          <span>已瀏覽至本篇底部</span>
          <button class="button secondary" type="button" :disabled="busy||!next" @click="selectThread(next)">後一篇<ChevronRight :size="16"/></button>
        </nav>
      </main>
      <main v-else class="thread-reader empty-reader"><MessagesSquare :size="36"/><h3>從第一篇討論開始</h3><p class="muted">發起討論後，內容會顯示在這裡。</p></main>
    </div>
  </section>
</template>

<style scoped>
.discussion-panel{padding:0;overflow:hidden;min-width:0}.discussion-heading{display:flex;align-items:center;justify-content:space-between;gap:18px;padding:22px 24px;border-bottom:1px solid var(--line)}.discussion-heading h2{display:flex;align-items:center;gap:9px}.discussion-heading p{margin-top:4px}.discussion-notice{margin:14px 24px 0;padding:11px 14px;border-radius:8px;background:color-mix(in srgb,var(--green) 8%,var(--surface));color:var(--green)}.discussion-panel>.error-box{margin:14px 24px 0}.discussion-layout{display:grid;grid-template-columns:minmax(260px,330px) minmax(0,1fr);min-height:620px}.thread-sidebar{border-right:1px solid var(--line);background:var(--surface-soft);min-width:0}.thread-sidebar-heading{display:flex;align-items:center;justify-content:space-between;gap:10px;padding:16px 17px;border-bottom:1px solid var(--line)}.thread-sidebar-heading>div{display:flex;align-items:center;gap:8px;font-size:12px}.thread-sidebar-heading span{font-size:10px;color:var(--muted)}.new-thread-button{flex-shrink:0;padding:8px 11px;min-height:34px}.new-thread-form{display:grid;gap:12px;padding:16px;border-bottom:1px solid var(--line);background:var(--surface)}.new-thread-form label{font-size:11px}.new-thread-form input,.new-thread-form textarea{margin-top:6px}.file-label{display:flex!important;align-items:center;gap:5px;flex-wrap:wrap}.file-label input{font-size:10px}.thread-list{max-height:760px;overflow:auto}.thread-row{position:relative;display:grid;width:100%;gap:7px;padding:16px 18px;text-align:left;background:transparent;color:var(--text);border-bottom:1px solid var(--line)}.thread-row:hover{background:var(--accent-soft)}.thread-row.active{background:var(--surface);box-shadow:inset 3px 0 var(--accent)}.thread-row-title{font-size:12px;font-weight:700;line-height:1.55;overflow-wrap:anywhere}.thread-row-meta{display:flex;align-items:center;gap:10px;color:var(--muted);font-size:10px}.thread-row-meta span{display:inline-flex;align-items:center;gap:3px}.closed-label{display:inline-flex;align-items:center;gap:4px;color:var(--muted);font-size:9px}.empty-thread-list{padding:30px 18px;text-align:center;color:var(--muted)}.thread-reader{min-width:0;padding:22px 26px}.thread-navigation{display:grid;grid-template-columns:1fr auto 1fr;align-items:center;gap:12px;margin-bottom:22px}.thread-navigation>span{text-align:center;font-size:10px;color:var(--muted)}.thread-navigation .button:first-child{justify-self:start}.thread-navigation .button:last-child{justify-self:end}.thread-original{border-bottom:1px solid var(--line);padding-bottom:22px}.thread-title-line{display:flex;align-items:flex-start;justify-content:space-between;gap:18px}.thread-title-line h2{font-size:21px;overflow-wrap:anywhere}.closed-badge,.moderator-badge{display:inline-flex;align-items:center;gap:5px;border-radius:999px;white-space:nowrap}.closed-badge{padding:6px 9px;background:var(--surface-soft);color:var(--muted);font-size:10px}.moderator-badge{padding:3px 7px;background:var(--accent-soft);color:var(--accent);font-size:9px;font-weight:700}.author-line{display:flex;align-items:center;gap:8px;flex-wrap:wrap;margin-top:13px;font-size:11px}.author-line time{color:var(--muted);font-size:9px}.thread-body{margin-top:19px;font-size:14px;line-height:2}.attachment-link{display:inline-flex;align-items:center;gap:6px;margin-top:14px;color:var(--accent);font-size:11px}.thread-actions{display:flex;align-items:center;justify-content:space-between;gap:10px;margin-top:20px}.moderator-action{margin-left:auto}.reply-list{display:grid;gap:12px;padding:23px 0}.reply-list>h3{margin-bottom:2px}.discussion-reply{padding:16px 18px;border:1px solid var(--line);border-radius:9px;background:var(--surface-soft)}.discussion-reply.moderator-reply{border-left:3px solid var(--accent);background:color-mix(in srgb,var(--accent) 4%,var(--surface))}.discussion-reply .author-line{margin-top:0}.discussion-reply>p{margin-top:12px}.reply-like{display:inline-flex;align-items:center;gap:5px;margin-top:12px;padding:5px 8px;border-radius:6px;background:transparent;color:var(--muted);font-size:10px}.reply-like[aria-pressed="true"]{background:var(--accent-soft);color:var(--accent)}.empty-replies{padding:22px;text-align:center;color:var(--muted);background:var(--surface-soft);border-radius:8px}.reply-form{padding:19px;border:1px solid var(--line);border-radius:10px;background:var(--surface-soft)}.reply-form .button{margin-top:11px}.closed-explanation{display:flex;align-items:center;gap:7px;padding:15px;border-radius:8px;background:var(--surface-soft);color:var(--muted)}.bottom-navigation{padding-top:24px;margin-top:24px;margin-bottom:0;border-top:1px solid var(--line)}.empty-reader{display:grid;place-content:center;text-align:center;gap:8px;color:var(--muted)}
@media(max-width:900px){.discussion-layout{grid-template-columns:minmax(230px,285px) minmax(0,1fr)}.thread-reader{padding:18px}.thread-title-line h2{font-size:18px}}
@media(max-width:680px){.discussion-heading{align-items:flex-start;padding:18px;flex-wrap:wrap}.discussion-layout{display:block;min-height:0}.thread-sidebar{border-right:0;border-bottom:1px solid var(--line)}.thread-list{max-height:340px}.thread-reader{padding:18px 14px}.thread-navigation .button{padding:8px 10px;font-size:10px}.thread-title-line{display:block}.closed-badge{margin-top:10px}.thread-actions{align-items:stretch;flex-direction:column}.moderator-action{margin-left:0}.bottom-navigation>span{font-size:9px}.discussion-notice,.discussion-panel>.error-box{margin-left:14px;margin-right:14px}}
</style>
