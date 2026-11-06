import { reactive } from 'vue'
export const experience = reactive({ student: false, canSwitch: false, userId: null, contextCanPractice:null })
export function bindExperience(user) {
  experience.userId = user?.id || null
  experience.canSwitch = !!user?.canTeach
  experience.student = experience.canSwitch && localStorage.getItem(`nsysu-view:${user.id}`) === 'student'
}
export function setStudentExperience(value) {
  if (!experience.canSwitch) return
  experience.student = !!value
  localStorage.setItem(`nsysu-view:${experience.userId}`, value ? 'student' : 'teaching')
}

export function setCourseExperience(value){experience.contextCanPractice=value}
