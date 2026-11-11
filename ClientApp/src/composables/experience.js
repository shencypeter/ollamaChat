import { reactive } from 'vue'
export const experience = reactive({ student: false, canSwitch: false, userId: null, contextCanPractice:null })
export function bindExperience(user) {
  if(!user && experience.userId)sessionStorage.removeItem('preview-course:'+experience.userId)
  experience.userId = user?.id || null
  experience.canSwitch = !!user?.canTeach
  experience.student = false
}
export function setStudentExperience(value) {
  if (!experience.canSwitch) return
  experience.student = !!value
}

export function setCourseExperience(value){experience.contextCanPractice=value}
