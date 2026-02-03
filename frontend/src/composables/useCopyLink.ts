import { onBeforeUnmount, ref, type Ref } from 'vue'

export function useCopyLink(roomLink: Ref<string>) {
  const copyStatus = ref('')
  let copyStatusTimer: number | null = null

  const setCopyStatus = (message: string) => {
    copyStatus.value = message
    if (copyStatusTimer) {
      window.clearTimeout(copyStatusTimer)
      copyStatusTimer = null
    }
    copyStatusTimer = window.setTimeout(() => {
      copyStatus.value = ''
      copyStatusTimer = null
    }, 3000)
  }

  const copyText = (text: string) => {
    const textarea = document.createElement('textarea')
    textarea.value = text
    textarea.setAttribute('readonly', '')
    textarea.style.position = 'fixed'
    textarea.style.top = '-1000px'
    textarea.style.left = '-1000px'
    document.body.appendChild(textarea)
    textarea.select()
    textarea.setSelectionRange(0, text.length)
    const succeeded = typeof document.execCommand === 'function' && document.execCommand('copy')
    document.body.removeChild(textarea)
    return succeeded
  }

  async function copyLink() {
    copyStatus.value = ''
    if (copyStatusTimer) {
      window.clearTimeout(copyStatusTimer)
      copyStatusTimer = null
    }
    const link = roomLink.value
    const succeeded = copyText(link)
    if (succeeded) {
      setCopyStatus('Link copied.')
    } else {
      window.prompt('Copy this room link:', link)
    }
  }

  onBeforeUnmount(() => {
    if (copyStatusTimer) {
      window.clearTimeout(copyStatusTimer)
    }
  })

  return {
    copyStatus,
    copyLink,
  }
}
