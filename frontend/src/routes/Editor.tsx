import { Link } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { noteSchema } from "../validation";
import type { NoteInput } from "../validation";
import { isAxiosError } from "axios";
import { api } from "../lib/api";
import { useToast } from "../lib/toast";
import { initTelegramUI, setMainButton, hideMainButton, hasTelegramUI } from "../lib/telegram";
import { useCallback, useEffect } from "react";

export default function Editor() {
  const { register, handleSubmit, formState } = useForm<NoteInput>({
    resolver: zodResolver(noteSchema),
    defaultValues: { text: "" }
  });
  const { Toast, show } = useToast();

  useEffect(() => { initTelegramUI(); }, []);
  const onSubmit = useCallback(handleSubmit(async (data) => {
    try {
      await api.post("/textpad", data);
      show("Сохранено");
    } catch (error: unknown) {
      type ApiErrorResponse = { title?: string };
      if (isAxiosError<ApiErrorResponse>(error)) {
        const msg = error.response?.data?.title;
        show(msg ?? "Ошибка сохранения");
      } else {
        show("Ошибка сохранения");
      }
    }
  }), [handleSubmit, show]);

  useEffect(() => {
    setMainButton(onSubmit);
    return hideMainButton;
  }, [onSubmit]);

  return (
    <div className="container">
      <h1>TextPad</h1>
      <p><Link to="/list">Перейти к записям →</Link></p>
      <textarea placeholder="Введите текст…" {...register("text")} />
      {formState.errors.text && <p>{formState.errors.text.message}</p>}
      {!hasTelegramUI() && (
        <button type="button" onClick={onSubmit} disabled={formState.isSubmitting}>
          Сохранить
        </button>
      )}
      <Toast />
    </div>
  );
}
