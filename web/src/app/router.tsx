import { createBrowserRouter } from "react-router";
import { CALLBACK_PATH } from "@/lib/auth/oidc";
import { CallbackPage, LogoutPage, NotFoundPage, WelcomePage } from "./pages/PublicPages";
import { RoleGate } from "./RoleGate";

// Role pages load on demand to keep the first download small (docs/09 §6: < 250 KB gzip, other routes lazy).
const homePages = () => import("@/features/home/routes/HomePages");
const profilePage = () => import("@/features/identity/routes/ProfilePage");
const adminUsersPage = () => import("@/features/identity/routes/AdminUsersPage");
const adminContentPage = () => import("@/features/content/routes/AdminContentPage");
const adminReferencePage = () => import("@/features/content/routes/AdminReferencePage");
const materialsHubPage = () => import("@/features/content/routes/MaterialsHubPage");
const chapterPage = () => import("@/features/content/routes/ChapterPage");
const classListPage = () => import("@/features/classroom/routes/ClassListPage");
const classDetailPage = () => import("@/features/classroom/routes/ClassDetailPage");
const studentClassPage = () => import("@/features/classroom/routes/StudentClassPage");
const practiceSetupPage = () => import("@/features/practice/routes/PracticeSetupPage");
const drillPage = () => import("@/features/practice/routes/DrillPage");
const schedulePage = () => import("@/features/classroom/routes/SchedulePage");
const weaknessesPage = () => import("@/features/practice/routes/WeaknessesPage");
const questionBankPage = () => import("@/features/practice/routes/QuestionBankPage");
const sourcesPage = () => import("@/features/practice/routes/SourcesPage");
const studentProgressPage = () => import("@/features/practice/routes/StudentProgressPage");
const quizListPage = () => import("@/features/practice/routes/QuizListPage");
const quizAttemptPage = () => import("@/features/practice/routes/QuizAttemptPage");
const assignmentsPage = () => import("@/features/practice/routes/AssignmentsPage");
const myGradesPage = () => import("@/features/practice/routes/MyGradesPage");

const staffPracticeRoutes = [
  { path: "bank-soal", lazy: async () => ({ Component: (await questionBankPage()).QuestionBankPage }) },
  {
    path: "murid/:studentId",
    lazy: async () => ({ Component: (await studentProgressPage()).StudentProgressPage }),
  },
];

/** Routes of docs/07 §12; each phase adds its pages under the role it belongs to. */
export const router = createBrowserRouter([
  { path: "/", element: <WelcomePage /> },
  { path: CALLBACK_PATH, element: <CallbackPage /> },
  { path: "/keluar", element: <LogoutPage /> },
  {
    path: "/murid/latihan/:sessionId",
    element: <RoleGate requiredRole="murid" focus />,
    children: [{ index: true, lazy: async () => ({ Component: (await drillPage()).DrillPage }) }],
  },
  {
    path: "/murid/kuis/:attemptId",
    element: <RoleGate requiredRole="murid" focus />,
    children: [{ index: true, lazy: async () => ({ Component: (await quizAttemptPage()).QuizAttemptPage }) }],
  },
  {
    path: "/murid",
    element: <RoleGate requiredRole="murid" />,
    children: [
      { index: true, lazy: async () => ({ Component: (await homePages()).MuridHomePage }) },
      { path: "kelas", lazy: async () => ({ Component: (await classListPage()).ClassListPage }) },
      {
        path: "kelas/:classId",
        lazy: async () => ({ Component: (await studentClassPage()).StudentClassPage }),
      },
      { path: "latihan", lazy: async () => ({ Component: (await practiceSetupPage()).PracticeSetupPage }) },
      { path: "kelemahan", lazy: async () => ({ Component: (await weaknessesPage()).WeaknessesPage }) },
      { path: "kuis", lazy: async () => ({ Component: (await quizListPage()).QuizListPage }) },
      { path: "tugas", lazy: async () => ({ Component: (await assignmentsPage()).AssignmentListPage }) },
      {
        path: "tugas/:assignmentId",
        lazy: async () => ({ Component: (await assignmentsPage()).AssignmentPage }),
      },
      { path: "nilai", lazy: async () => ({ Component: (await myGradesPage()).MyGradesPage }) },
      { path: "materi", lazy: async () => ({ Component: (await materialsHubPage()).MaterialsHubPage }) },
      { path: "bab/:chapterId", lazy: async () => ({ Component: (await chapterPage()).ChapterPage }) },
      { path: "jadwal", lazy: async () => ({ Component: (await schedulePage()).SchedulePage }) },
      { path: "profil", lazy: async () => ({ Component: (await profilePage()).ProfilePage }) },
    ],
  },
  {
    path: "/sensei",
    element: <RoleGate requiredRole="sensei" />,
    children: [
      { index: true, lazy: async () => ({ Component: (await homePages()).SenseiHomePage }) },
      { path: "kelas", lazy: async () => ({ Component: (await classListPage()).ClassListPage }) },
      {
        path: "kelas/:classId/:tab?",
        lazy: async () => ({ Component: (await classDetailPage()).ClassDetailPage }),
      },
      { path: "jadwal", lazy: async () => ({ Component: (await schedulePage()).SchedulePage }) },
      ...staffPracticeRoutes,
      { path: "profil", lazy: async () => ({ Component: (await profilePage()).ProfilePage }) },
    ],
  },
  {
    path: "/admin",
    element: <RoleGate requiredRole="admin" />,
    children: [
      { index: true, lazy: async () => ({ Component: (await homePages()).AdminHomePage }) },
      { path: "pengguna", lazy: async () => ({ Component: (await adminUsersPage()).AdminUsersPage }) },
      { path: "konten", lazy: async () => ({ Component: (await adminContentPage()).AdminContentPage }) },
      {
        path: "referensi",
        lazy: async () => ({ Component: (await adminReferencePage()).AdminReferencePage }),
      },
      { path: "kelas", lazy: async () => ({ Component: (await classListPage()).ClassListPage }) },
      {
        path: "kelas/:classId/:tab?",
        lazy: async () => ({ Component: (await classDetailPage()).ClassDetailPage }),
      },
      ...staffPracticeRoutes,
      { path: "sumber-soal", lazy: async () => ({ Component: (await sourcesPage()).SourcesPage }) },
      { path: "profil", lazy: async () => ({ Component: (await profilePage()).ProfilePage }) },
    ],
  },
  { path: "*", element: <NotFoundPage /> },
]);
