import { RotateCcw } from "lucide-react";
import { useEffect, useState } from "react";
import { toKana } from "wanakana";
import { Button } from "@/components/ui/button";
import { Field, FieldHint, FieldLabel, Input } from "@/components/ui/form";
import { drill } from "@/i18n/labels/practice";
import { usePrefs, useT } from "@/i18n/prefs";
import { textIn } from "@/i18n/textblock";
import { cn } from "@/lib/utils";
import type { SafeQuestion } from "../api/practice";
import { QuestionText } from "./QuestionText";

/** Answer shapes of docs/03 Â§8.2. */
export type DrillAnswer =
  | { selected: number[] }
  | { order: number[] }
  | { text: string; raw?: string }
  | { pairs: number[][] }
  | { remembered: boolean };

interface ControlProps {
  question: SafeQuestion;
  /** Disabled once answered; the key arrives with the feedback. */
  disabled: boolean;
  onAnswer: (answer: DrillAnswer) => void;
}

/** Keyboard shortcuts while a question is active (docs/07 Â§11 flow 3); ignored while typing in a field. */
function useShortcut(active: boolean, handler: (event: KeyboardEvent) => void) {
  useEffect(() => {
    if (!active) return;
    const listener = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null;
      if (target && (target.tagName === "INPUT" || target.tagName === "TEXTAREA")) return;
      handler(event);
    };
    window.addEventListener("keydown", listener);
    return () => {
      window.removeEventListener("keydown", listener);
    };
  }, [active, handler]);
}

const optionButton = [
  "flex min-h-12 w-full items-center gap-3 rounded-md border border-sumi-300 bg-surface px-4 py-3 text-left text-body text-ink",
  "hover:bg-canvas-subtle focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2",
  "disabled:cursor-not-allowed disabled:opacity-70",
];

function ShortcutNumber({ n }: { n: number }) {
  return (
    <span
      aria-hidden="true"
      className="inline-flex size-6 shrink-0 items-center justify-center rounded-sm bg-canvas-muted text-caption font-semibold text-ink-muted"
    >
      {n}
    </span>
  );
}

export function AnswerControls(props: ControlProps) {
  switch (props.question.kind) {
    case "mcq":
      return <ChoiceAnswer {...props} />;
    case "tap_sentence":
      return <TapAnswer {...props} />;
    case "type_answer":
      return <TypedAnswer {...props} />;
    case "match":
      return <MatchAnswer {...props} />;
    case "flashcard":
      return <FlashcardAnswer {...props} />;
    default:
      return null;
  }
}

function ChoiceAnswer({ question, disabled, onAnswer }: ControlProps) {
  const t = useT();
  const [selected, setSelected] = useState<number[]>([]);
  const choices = question.choices ?? [];
  const multi = question.multi === true;
  const display = question.jaDisplay;

  const pick = (choiceId: number) => {
    if (disabled) return;
    if (!multi) {
      onAnswer({ selected: [choiceId] });
      return;
    }
    setSelected((current) =>
      current.includes(choiceId) ? current.filter((id) => id !== choiceId) : [...current, choiceId],
    );
  };

  useShortcut(!disabled, (event) => {
    const index = Number(event.key) - 1;
    const choice = choices[index];
    if (choice !== undefined && index < 6) {
      event.preventDefault();
      pick(choice.choiceId);
    } else if (event.key === "Enter" && multi && selected.length > 0) {
      event.preventDefault();
      onAnswer({ selected });
    }
  });

  return (
    <div className="flex flex-col gap-3">
      {multi && <p className="text-body-sm text-ink-muted">{t(drill.multiHint)}</p>}
      <ul className="grid gap-3 sm:grid-cols-2" aria-label={t(drill.choices)}>
        {choices.map((choice, index) => {
          const isSelected = selected.includes(choice.choiceId);
          return (
            <li key={choice.choiceId}>
              <button
                type="button"
                disabled={disabled}
                aria-pressed={multi ? isSelected : undefined}
                className={cn(optionButton, isSelected && "border-2 border-sumi-900")}
                onClick={() => {
                  pick(choice.choiceId);
                }}
              >
                {index < 6 && <ShortcutNumber n={index + 1} />}
                <QuestionText value={choice.text} display={display} className="items-start" />
              </button>
            </li>
          );
        })}
      </ul>
      {multi && (
        <Button
          variant="primary"
          className="self-end"
          disabled={disabled || selected.length === 0}
          onClick={() => {
            onAnswer({ selected });
          }}
        >
          {t(drill.submit)}
        </Button>
      )}
    </div>
  );
}

function TapAnswer({ question, disabled, onAnswer }: ControlProps) {
  const t = useT();
  const [order, setOrder] = useState<number[]>([]);
  const tokens = question.tokens ?? [];
  const display = question.jaDisplay;
  const byId = new Map(tokens.map((token) => [token.tokenId, token]));

  useShortcut(!disabled, (event) => {
    if (event.key === "Enter" && order.length > 0) {
      event.preventDefault();
      onAnswer({ order });
    }
  });

  return (
    <div className="flex flex-col gap-4">
      <p className="text-body-sm text-ink-muted">{t(drill.tapHint)}</p>
      <section
        aria-label={t(drill.yourSentence)}
        className="min-h-16 rounded-lg border-2 border-dashed border-line-strong p-3"
      >
        <ol className="flex flex-wrap gap-2">
          {order.map((tokenId) => {
            const token = byId.get(tokenId);
            return token ? (
              <li key={tokenId}>
                <button
                  type="button"
                  disabled={disabled}
                  className="min-h-11 rounded-md border border-sumi-900 bg-surface px-3 py-2 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
                  onClick={() => {
                    setOrder(order.filter((id) => id !== tokenId));
                  }}
                >
                  <QuestionText value={token.text} display={display} />
                </button>
              </li>
            ) : null;
          })}
        </ol>
      </section>
      <section aria-label={t(drill.words)}>
        <ul className="flex flex-wrap gap-2">
          {tokens
            .filter((token) => !order.includes(token.tokenId))
            .map((token) => (
              <li key={token.tokenId}>
                <button
                  type="button"
                  disabled={disabled}
                  className="min-h-11 rounded-md border border-sumi-300 bg-surface px-3 py-2 hover:bg-canvas-subtle focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
                  onClick={() => {
                    setOrder([...order, token.tokenId]);
                  }}
                >
                  <QuestionText value={token.text} display={display} />
                </button>
              </li>
            ))}
        </ul>
      </section>
      <div className="flex justify-end gap-2">
        <Button
          variant="ghost"
          disabled={disabled || order.length === 0}
          onClick={() => {
            setOrder([]);
          }}
        >
          <RotateCcw aria-hidden="true" />
          {t(drill.reset)}
        </Button>
        <Button
          variant="primary"
          disabled={disabled || order.length === 0}
          onClick={() => {
            onAnswer({ order });
          }}
        >
          {t(drill.submit)}
        </Button>
      </div>
    </div>
  );
}

function TypedAnswer({ question, disabled, onAnswer }: ControlProps) {
  const t = useT();
  const [raw, setRaw] = useState("");
  const kana = question.answerScript === "kana";
  // BR-DRILL-06 step 3: romaji typed for a kana answer is converted on the client (WanaKana).
  const converted = kana ? toKana(raw) : raw;
  const submit = () => {
    if (disabled || raw.trim() === "") return;
    onAnswer(kana ? { text: converted, raw } : { text: raw });
  };

  return (
    <form
      noValidate
      className="flex flex-col gap-3"
      onSubmit={(event) => {
        event.preventDefault();
        submit();
      }}
    >
      <Field>
        <FieldLabel htmlFor="drill-typed">{t(drill.typeLabel)}</FieldLabel>
        <Input
          id="drill-typed"
          value={raw}
          disabled={disabled}
          autoComplete="off"
          autoCapitalize="none"
          autoCorrect="off"
          spellCheck={false}
          lang={kana ? "ja" : undefined}
          className="h-12 text-h3"
          aria-describedby="drill-typed-hint"
          // eslint-disable-next-line jsx-a11y/no-autofocus -- the question was just shown and expects typing
          autoFocus
          onChange={(event) => {
            setRaw(event.target.value);
          }}
        />
        <FieldHint id="drill-typed-hint">{t(kana ? drill.typeKanaHint : drill.typeRomajiHint)}</FieldHint>
        {kana && raw !== "" && (
          <p className="font-ja text-h3 text-ink" lang="ja" aria-live="polite">
            {converted}
          </p>
        )}
      </Field>
      <Button variant="primary" type="submit" className="self-end" disabled={disabled || raw.trim() === ""}>
        {t(drill.submit)}
      </Button>
    </form>
  );
}

function MatchAnswer({ question, disabled, onAnswer }: ControlProps) {
  const t = useT();
  const { uiLocale } = usePrefs();
  const left = question.left ?? [];
  const right = question.right ?? [];
  const display = question.jaDisplay;
  const [pairs, setPairs] = useState<Map<number, number>>(new Map());
  const [active, setActive] = useState<number | null>(null);
  const usedRight = new Set(pairs.values());
  const labelOf = (block: (typeof left)[number]["text"]) =>
    textIn(block, uiLocale) || (block.ja ?? []).map((segment) => segment.t).join("");

  const chooseRight = (rightId: number) => {
    if (active === null || disabled) return;
    const next = new Map(pairs);
    for (const [key, value] of next) if (value === rightId) next.delete(key);
    next.set(active, rightId);
    setPairs(next);
    setActive(null);
  };

  return (
    <div className="flex flex-col gap-4">
      <p className="text-body-sm text-ink-muted">{t(drill.matchHint)}</p>
      <div className="grid grid-cols-2 gap-3">
        <ul className="flex flex-col gap-2">
          {left.map((item) => {
            const paired = pairs.get(item.leftId);
            const partner = right.find((option) => option.rightId === paired);
            return (
              <li key={item.leftId}>
                <button
                  type="button"
                  disabled={disabled}
                  aria-pressed={active === item.leftId}
                  className={cn(
                    optionButton,
                    active === item.leftId && "border-2 border-sumi-900",
                    paired !== undefined && "bg-canvas-subtle",
                  )}
                  onClick={() => {
                    if (paired !== undefined) {
                      const next = new Map(pairs);
                      next.delete(item.leftId);
                      setPairs(next);
                    }
                    setActive(item.leftId);
                  }}
                >
                  <span className="flex flex-col">
                    <QuestionText value={item.text} display={display} className="items-start" />
                    {partner && (
                      <span className="text-caption text-ink-muted">â†’ {labelOf(partner.text)}</span>
                    )}
                  </span>
                </button>
              </li>
            );
          })}
        </ul>
        <ul className="flex flex-col gap-2">
          {right.map((item) => (
            <li key={item.rightId}>
              <button
                type="button"
                disabled={disabled || active === null}
                className={cn(optionButton, usedRight.has(item.rightId) && "bg-canvas-subtle text-ink-muted")}
                aria-label={
                  active === null
                    ? undefined
                    : t(drill.pairWith, {
                        left: labelOf(
                          left.find((entry) => entry.leftId === active)?.text ?? {
                            id: null,
                            en: null,
                            ja: null,
                          },
                        ),
                        right: labelOf(item.text),
                      })
                }
                onClick={() => {
                  chooseRight(item.rightId);
                }}
              >
                <QuestionText value={item.text} display={display} className="items-start" />
              </button>
            </li>
          ))}
        </ul>
      </div>
      <div className="flex justify-end gap-2">
        <Button
          variant="ghost"
          disabled={disabled || pairs.size === 0}
          onClick={() => {
            setPairs(new Map());
            setActive(null);
          }}
        >
          <RotateCcw aria-hidden="true" />
          {t(drill.reset)}
        </Button>
        <Button
          variant="primary"
          disabled={disabled || pairs.size !== left.length}
          onClick={() => {
            onAnswer({ pairs: [...pairs.entries()].map(([leftId, rightId]) => [leftId, rightId]) });
          }}
        >
          {t(drill.submit)}
        </Button>
      </div>
    </div>
  );
}

function FlashcardAnswer({ question, disabled, onAnswer }: ControlProps) {
  const t = useT();
  const [flipped, setFlipped] = useState(false);
  const display = question.jaDisplay;

  useShortcut(!disabled, (event) => {
    if (event.key === " ") {
      event.preventDefault();
      setFlipped((value) => !value);
    }
  });

  return (
    <div className="flex flex-col items-center gap-4">
      <button
        type="button"
        disabled={disabled}
        aria-label={t(drill.flip)}
        aria-pressed={flipped}
        className="flex min-h-48 w-full max-w-md flex-col items-center justify-center gap-2 rounded-xl border border-line bg-surface p-6 shadow-sm focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-none"
        onClick={() => {
          setFlipped(!flipped);
        }}
      >
        <QuestionText value={flipped ? question.back : question.front} display={display} large />
      </button>
      {flipped && (
        <div className="flex gap-3">
          <Button
            variant="outline"
            disabled={disabled}
            onClick={() => {
              onAnswer({ remembered: false });
            }}
          >
            {t(drill.notYet)}
          </Button>
          <Button
            variant="primary"
            disabled={disabled}
            onClick={() => {
              onAnswer({ remembered: true });
            }}
          >
            {t(drill.remembered)}
          </Button>
        </div>
      )}
    </div>
  );
}
