import { MessageSquare, Target } from "lucide-react";
import { Link } from "react-router";
import { Label } from "@/components/japanese/Label";
import { buttonVariants } from "@/components/ui/button-variants";
import { EmptyState, PageHeader, Skeleton } from "@/components/ui/misc";
import { progress, setup, weaknesses } from "@/i18n/labels/practice";
import { useT } from "@/i18n/prefs";
import { useComments, useMyWeaknesses } from "../api/practice";
import { CommentList } from "../components/Comments";
import { WeaknessList } from "../components/WeaknessList";

/** /murid/kelemahan — "Kelemahanku" (BR-MASTERY-03) and the comments of the student's teachers. */
export function WeaknessesPage() {
  const t = useT();
  const data = useMyWeaknesses();
  const comments = useComments({});

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={<Label value={weaknesses.title} companionClassName="text-body font-medium" />}
        description={t(weaknesses.description)}
      />
      {data.isPending && <Skeleton className="h-48" />}
      {data.data && data.data.groups.length === 0 && (
        <EmptyState
          icon={Target}
          title={<Label value={weaknesses.empty} />}
          action={
            <Link to="/murid/latihan" className={buttonVariants({ variant: "primary" })}>
              {t(setup.start)}
            </Link>
          }
        />
      )}
      {data.data && data.data.groups.length > 0 && <WeaknessList data={data.data} practise />}
      <section className="flex flex-col gap-3" aria-labelledby="my-comments">
        <h2 id="my-comments" className="flex items-center gap-2 text-body font-bold text-ink">
          <MessageSquare className="size-4" aria-hidden="true" />
          <Label
            value={progress.myComments}
            companion="inline"
            companionClassName="text-body-sm font-medium"
          />
        </h2>
        <CommentList comments={comments.data} loading={comments.isPending} />
      </section>
    </div>
  );
}
