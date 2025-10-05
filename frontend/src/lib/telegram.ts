// Типы Telegram WebApp SDK минимально
declare global {
  interface Window {
    Telegram: any
  }
}

export const tg = window.Telegram?.WebApp

// Инициализация; тема; кнопки
export function initTelegramUI() {
  tg?.ready()
  tg?.expand()
  tg?.MainButton?.setText("Сохранить")
  tg?.BackButton?.onClick(() => window.history.back())
}

export function setMainButton(onClick: () => void) {
  tg?.MainButton?.show()
  tg?.MainButton?.onClick(onClick)
}

export function hideMainButton() {
  tg?.MainButton?.hide()
}

export function getInitData(): string {
  return tg?.initData || ""
}
