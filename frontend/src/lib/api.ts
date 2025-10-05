import axios from "axios"
import { getInitData } from "./telegram"

const baseURL = import.meta.env.VITE_BACKEND_BASE_URL || "http://localhost:8080"

export const api = axios.create({ baseURL })

api.interceptors.request.use((cfg) => {
  cfg.headers = cfg.headers || {}
  cfg.headers["X-Telegram-Init-Data"] = getInitData()
  cfg.headers["X-Request-ID"] = crypto.randomUUID()
  return cfg
})
