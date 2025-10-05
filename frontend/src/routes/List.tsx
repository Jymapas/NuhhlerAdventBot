import { useEffect, useMemo } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { api } from "../lib/api";
import { initTelegramUI } from "../lib/telegram";

type NoteDto = {
  id: number;
  text: string;
  updatedAt: string;
};

export default function List() {
  useEffect(() => { initTelegramUI(); }, []);

  const { data: notes = [], isLoading, isError } = useQuery<NoteDto[]>({
    queryKey: ["notes"],
    queryFn: async () => {
      const res = await api.get<NoteDto[]>("/textpad", { params: { skip: 0, take: 20 } });
      return res.data;
    },
  });

  const formatter = useMemo(() => new Intl.DateTimeFormat(undefined, {
    dateStyle: "short",
    timeStyle: "short",
  }), []);

  if (isLoading) return <div className="container">Загрузка…</div>;
  if (isError) return <div className="container">Ошибка загрузки</div>;

  return (
    <div className="container">
      <h1>Мои записи</h1>
      {notes.length === 0 ? (
        <p>Пока пусто. Создайте первую запись в редакторе.</p>
      ) : (
        <div className="list">
          {notes.map((note) => (
            <div className="card" key={note.id}>
              <div style={{ whiteSpace: "pre-wrap" }}>{note.text}</div>
              <small>Обновлено: {formatter.format(new Date(note.updatedAt))}</small>
            </div>
          ))}
        </div>
      )}
      <p><Link to="/">← к редактору</Link></p>
    </div>
  );
}
