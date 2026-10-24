import { NavLink } from "react-router";
import { cn } from "@/lib/utils";

export interface TabNavItem {
  to: string;
  label: string;
}

/**
 * Tabs whose state lives in the URL (e.g. /sensei/kelas/:classId/:tab), so every tab can be linked and reloaded.
 * Active tab: red underline 2 px and semibold ink text (docs/07 §10).
 */
export function TabNav({ items, label }: { items: readonly TabNavItem[]; label: string }) {
  return (
    <nav aria-label={label} className="-mx-4 overflow-x-auto px-4 sm:mx-0 sm:px-0">
      <ul className="flex min-w-max gap-1 border-b border-line">
        {items.map((item) => (
          <li key={item.to}>
            <NavLink
              to={item.to}
              className={({ isActive }) =>
                cn(
                  "-mb-px inline-flex h-11 items-center border-b-2 px-3 text-body-sm",
                  "focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none",
                  isActive
                    ? "border-action font-semibold text-ink"
                    : "border-transparent text-ink-muted hover:text-ink",
                )
              }
            >
              {item.label}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
