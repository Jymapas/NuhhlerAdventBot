import { useCallback, useEffect, useState } from "react";
export function useToast() {
  const [msg, setMsg] = useState<string | null>(null);
  const show = useCallback((m: string) => setMsg(m), []);
  useEffect(() => {
    if (!msg) return;
    const t = setTimeout(() => setMsg(null), 2500);
    return () => clearTimeout(t);
  }, [msg]);
  return {
    Toast: () => msg ? <div className="toast">{msg}</div> : null,
    show,
  };
}
