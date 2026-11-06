export function feedbackIsStale(feedback, revisionId, editorText, savedText) {
  return !!feedback && (feedback.isStale || feedback.answerRevisionId !== revisionId || editorText !== savedText)
}
export function formativeSubmissionWarning(questions, own, answers, feedback) {
  let missing = false, stale = false
  for (const question of questions) {
    const answer = own.answers.find(a => a.questionId === question.id), latest = feedback[question.id]
    if (latest && feedbackIsStale(latest, answer?.currentRevisionId, answers[question.id] || '', answer?.text || '')) stale = true
    else if (!latest || latest.status !== 'completed') missing = true
  }
  return stale ? '目前的 AI 回饋是根據較早版本產生，仍要提交嗎？' : missing ? '此版本尚未取得 AI 回饋，仍要提交嗎？' : ''
}
