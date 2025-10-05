import { BrowserRouter, Routes, Route } from "react-router-dom";
import { QueryClientProvider } from "@tanstack/react-query";
import { queryClient } from "./lib/queryClient";
import Editor from "./routes/Editor";
import List from "./routes/List";
import "./styles.css";

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<Editor />} />
          <Route path="/list" element={<List />} />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
