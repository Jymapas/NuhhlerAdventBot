import { z } from "zod"
export const noteSchema = z.object({
  text: z.string().min(1, "Текст обязателен").max(4000, "Максимум 4000"),
})
export type NoteInput = z.infer<typeof noteSchema>
