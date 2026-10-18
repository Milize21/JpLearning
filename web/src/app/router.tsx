import { createBrowserRouter } from "react-router";
import { CALLBACK_PATH } from "@/lib/auth/oidc";
import { CallbackPage, LogoutPage, NotFoundPage, WelcomePage } from "./pages/PublicPages";
import { RoleGate } from "./RoleGate";

// Role pages load on demand to keep the first download small (docs/09 §6: < 250 KB gzip, other routes lazy).
const homePages = () => import("@/features/home/routes/HomePages");
const profilePage = () => import("@/features/identity/routes/ProfilePage");
const adminUsersPage = () => import("@/features/identity/routes/AdminUsersPage");

/** Routes of docs/07 §12; each phase adds its pages under the role it belongs to. */
export const router = createBrowserRouter([
  { path: "/", element: <WelcomePage /> },
  { path: CALLBACK_PATH, element: <CallbackPage /> },
  { path: "/keluar", element: <LogoutPage /> },
  {
    path: "/murid",
    element: <RoleGate requiredRole="murid" />,
    children: [
      { index: true, lazy: async () => ({ Component: (await homePages()).MuridHomePage }) },
      { path: "profil", lazy: async () => ({ Component: (await profilePage()).ProfilePage }) },
    ],
  },
  {
    path: "/sensei",
    element: <RoleGate requiredRole="sensei" />,
    children: [
      { index: true, lazy: async () => ({ Component: (await homePages()).SenseiHomePage }) },
      { path: "profil", lazy: async () => ({ Component: (await profilePage()).ProfilePage }) },
    ],
  },
  {
    path: "/admin",
    element: <RoleGate requiredRole="admin" />,
    children: [
      { index: true, lazy: async () => ({ Component: (await homePages()).AdminHomePage }) },
      { path: "pengguna", lazy: async () => ({ Component: (await adminUsersPage()).AdminUsersPage }) },
      { path: "profil", lazy: async () => ({ Component: (await profilePage()).ProfilePage }) },
    ],
  },
  { path: "*", element: <NotFoundPage /> },
]);
