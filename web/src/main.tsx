import "@fontsource-variable/plus-jakarta-sans";
import "@fontsource-variable/noto-sans-jp";
import "@fontsource/klee-one/600.css";
import "./styles/globals.css";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { RouterProvider } from "react-router";
import { AppProviders } from "./app/providers";
import { router } from "./app/router";

const root = document.getElementById("root");
if (!root) throw new Error("#root is missing in index.html");

createRoot(root).render(
  <StrictMode>
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>
  </StrictMode>,
);
