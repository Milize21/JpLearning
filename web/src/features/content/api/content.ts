import { keepPreviousData, useMutation, useQueries, useQuery, useQueryClient } from "@tanstack/react-query";
import { contentApi } from "@/lib/api/client";
import type { components } from "@/lib/api/schema/content";

type Schemas = components["schemas"];

export type TextBlockDto = Schemas["TextBlock"];
export type RichTextDto = Schemas["RichText"];
export type LevelDto = Schemas["LevelDto"];
export type BookSummaryDto = Schemas["BookSummaryDto"];
export type BookDto = Schemas["BookDto"];
export type ChapterSummaryDto = Schemas["ChapterSummaryDto"];
export type ChapterDto = Schemas["ChapterDto"];
export type MaterialDto = Schemas["MaterialDto"];
export type MaterialKind = Schemas["MaterialKind"];
export type CreateMaterialRequest = Schemas["CreateMaterialRequest"];
export type UpdateMaterialRequest = Schemas["UpdateMaterialRequest"];
export type ImportReferenceResponse = Schemas["ImportReferenceResponse"];
export type ImportCellError = Schemas["ImportCellError"];
export type KanaDto = Schemas["KanaDto"];
export type KanjiDto = Schemas["KanjiDto"];
export type VocabDto = Schemas["VocabDto"];
export type GrammarPointDto = Schemas["GrammarPointDto"];
export type SentenceDto = Schemas["SentenceDto"];

/** The {type} of /reference/{type} (docs/05 §5). */
export type ReferenceType = "kana" | "kanji" | "vocab" | "grammar" | "sentences";
export const referenceTypes: readonly ReferenceType[] = ["kana", "kanji", "vocab", "grammar", "sentences"];

export interface ReferenceItems {
  kana: KanaDto;
  kanji: KanjiDto;
  vocab: VocabDto;
  grammar: GrammarPointDto;
  sentences: SentenceDto;
}

export interface ReferenceFilter {
  levelId?: string;
  chapterId?: string;
  page: number;
  pageSize: number;
}

export interface ReferencePage<T extends ReferenceType> {
  items: ReferenceItems[T][];
  page: number;
  pageSize: number;
  totalCount: number;
}

export const contentKeys = {
  all: ["content"] as const,
  levels: ["content", "levels"] as const,
  book: (id: string) => ["content", "books", id] as const,
  chapter: (id: string) => ["content", "chapters", id] as const,
  materials: (chapterId: string) => ["content", "materials", chapterId] as const,
  reference: (type: ReferenceType, filter: ReferenceFilter) =>
    ["content", "reference", type, filter] as const,
};

export function useLevels() {
  return useQuery({
    queryKey: contentKeys.levels,
    staleTime: 60_000,
    queryFn: async () => {
      const { data } = await contentApi.GET("/api/content/levels");
      return data as LevelDto[];
    },
  });
}

export function useBook(id: string | undefined) {
  return useQuery({
    queryKey: contentKeys.book(id ?? ""),
    enabled: id !== undefined,
    queryFn: async () => {
      const { data } = await contentApi.GET("/api/content/books/{id}", {
        params: { path: { id: id ?? "" } },
      });
      return data as BookDto;
    },
  });
}

/** Several books with their chapters, e.g. every book of a level or of the student's classes. */
export function useBooks(bookIds: readonly string[]) {
  return useQueries({
    queries: bookIds.map((id) => ({
      queryKey: contentKeys.book(id),
      queryFn: async () => {
        const { data } = await contentApi.GET("/api/content/books/{id}", { params: { path: { id } } });
        return data as BookDto;
      },
    })),
  });
}

export function useChapter(id: string | undefined) {
  return useQuery({
    queryKey: contentKeys.chapter(id ?? ""),
    enabled: id !== undefined,
    retry: false,
    queryFn: async () => {
      const { data } = await contentApi.GET("/api/content/chapters/{id}", {
        params: { path: { id: id ?? "" } },
      });
      return data as ChapterDto;
    },
  });
}

export function useMaterials(chapterId: string | undefined) {
  return useQuery({
    queryKey: contentKeys.materials(chapterId ?? ""),
    enabled: chapterId !== undefined,
    retry: false,
    queryFn: async () => {
      const { data } = await contentApi.GET("/api/content/chapters/{id}/materials", {
        params: { path: { id: chapterId ?? "" } },
      });
      return data as MaterialDto[];
    },
  });
}

function useInvalidateContent() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: contentKeys.all });
}

export interface LevelInput {
  code: string;
  name: TextBlockDto;
  sortOrder: number;
}

export function useSaveLevel() {
  const invalidate = useInvalidateContent();
  return useMutation({
    mutationFn: async ({ level, input }: { level?: LevelDto; input: LevelInput }) => {
      if (level) {
        const { data } = await contentApi.PATCH("/api/content/levels/{id}", {
          params: { path: { id: level.id } },
          body: { version: level.version, ...input },
        });
        return data as LevelDto;
      }
      const { data } = await contentApi.POST("/api/content/levels", { body: input });
      return data as LevelDto;
    },
    onSuccess: invalidate,
  });
}

export interface BookInput {
  levelId: string;
  title: TextBlockDto;
  sortOrder: number;
}

export function useSaveBook() {
  const invalidate = useInvalidateContent();
  return useMutation({
    mutationFn: async ({ book, input }: { book?: BookSummaryDto; input: BookInput }) => {
      if (book) {
        await contentApi.PATCH("/api/content/books/{id}", {
          params: { path: { id: book.id } },
          body: { version: book.version, title: input.title, sortOrder: input.sortOrder, removeCover: false },
        });
        return;
      }
      await contentApi.POST("/api/content/books", { body: input });
    },
    onSuccess: invalidate,
  });
}

export interface ChapterInput {
  bookId: string;
  number: number;
  title: TextBlockDto;
  sortOrder: number;
}

export function useSaveChapter() {
  const invalidate = useInvalidateContent();
  return useMutation({
    mutationFn: async ({ chapter, input }: { chapter?: ChapterSummaryDto; input: ChapterInput }) => {
      if (chapter) {
        await contentApi.PATCH("/api/content/chapters/{id}", {
          params: { path: { id: chapter.id } },
          body: {
            version: chapter.version,
            number: input.number,
            title: input.title,
            sortOrder: input.sortOrder,
          },
        });
        return;
      }
      await contentApi.POST("/api/content/chapters", { body: input });
    },
    onSuccess: invalidate,
  });
}

export type StructureKind = "level" | "book" | "chapter";

export function useDeleteStructure() {
  const invalidate = useInvalidateContent();
  return useMutation({
    mutationFn: async ({ kind, id }: { kind: StructureKind; id: string }) => {
      const params = { params: { path: { id } } };
      if (kind === "level") await contentApi.DELETE("/api/content/levels/{id}", params);
      else if (kind === "book") await contentApi.DELETE("/api/content/books/{id}", params);
      else await contentApi.DELETE("/api/content/chapters/{id}", params);
    },
    onSuccess: invalidate,
  });
}

export function useCreateMaterial(chapterId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (request: CreateMaterialRequest) => {
      const { data } = await contentApi.POST("/api/content/chapters/{id}/materials", {
        params: { path: { id: chapterId } },
        body: request,
      });
      return data as MaterialDto;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: contentKeys.materials(chapterId) }),
  });
}

export function useUpdateMaterial(chapterId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, changes }: { id: string; changes: UpdateMaterialRequest }) => {
      const { data } = await contentApi.PATCH("/api/content/materials/{id}", {
        params: { path: { id } },
        body: changes,
      });
      return data as MaterialDto;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: contentKeys.materials(chapterId) }),
  });
}

export function useDeleteMaterial(chapterId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      await contentApi.DELETE("/api/content/materials/{id}", { params: { path: { id } } });
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: contentKeys.materials(chapterId) }),
  });
}

/**
 * BR-FILE-04: the API checks access and returns a signed link valid for a few minutes. The browser then opens the link
 * itself, which needs no token, and the file arrives as an attachment.
 */
export function useDownloadMaterial() {
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await contentApi.GET("/api/content/materials/{id}/download", {
        params: { path: { id }, query: { redirect: false } },
      });
      const url = (data as Schemas["DownloadLinkDto"]).url;
      window.location.assign(url);
    },
  });
}

export function useReference<T extends ReferenceType>(type: T, filter: ReferenceFilter, enabled = true) {
  return useQuery({
    queryKey: contentKeys.reference(type, filter),
    enabled,
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const query = {
        query: {
          levelId: filter.levelId,
          chapterId: filter.chapterId,
          page: filter.page,
          pageSize: filter.pageSize,
        },
      };
      switch (type) {
        case "kana":
          return (await contentApi.GET("/api/content/reference/kana", { params: query }))
            .data as ReferencePage<T>;
        case "kanji":
          return (await contentApi.GET("/api/content/reference/kanji", { params: query }))
            .data as ReferencePage<T>;
        case "vocab":
          return (await contentApi.GET("/api/content/reference/vocab", { params: query }))
            .data as ReferencePage<T>;
        case "grammar":
          return (await contentApi.GET("/api/content/reference/grammar", { params: query }))
            .data as ReferencePage<T>;
        default:
          return (await contentApi.GET("/api/content/reference/sentences", { params: query }))
            .data as ReferencePage<T>;
      }
    },
  });
}

export interface ImportReferenceInput {
  type: ReferenceType;
  file: File;
  source: string;
  license: string;
  levelId: string;
  bookId?: string;
  chapterId?: string;
  dryRun: boolean;
}

export function useImportReference() {
  const invalidate = useInvalidateContent();
  return useMutation({
    mutationFn: async (input: ImportReferenceInput) => {
      const { data } = await contentApi.POST("/api/content/reference/{type}/import", {
        params: { path: { type: input.type }, query: { dryRun: input.dryRun } },
        body: {} as never,
        bodySerializer: () => {
          const form = new FormData();
          form.append("file", input.file);
          form.append("source", input.source);
          form.append("license", input.license);
          form.append("levelId", input.levelId);
          if (input.bookId) form.append("bookId", input.bookId);
          if (input.chapterId) form.append("chapterId", input.chapterId);
          return form;
        },
      });
      return data as ImportReferenceResponse;
    },
    onSuccess: (result) => {
      if (!result.dryRun) void invalidate();
    },
  });
}

/** Maps an item to a chapter (or removes the mapping) so the question generator can use it (BR-KONTEN-04). */
export function useMapReferenceChapter() {
  const invalidate = useInvalidateContent();
  return useMutation({
    mutationFn: async ({
      type,
      id,
      chapterId,
    }: {
      type: ReferenceType;
      id: string;
      chapterId: string | null;
    }) => {
      const body = chapterId ? { chapterId, unmapChapter: false } : { unmapChapter: true };
      const params = { params: { path: { id } }, body };
      switch (type) {
        case "kana":
          await contentApi.PATCH("/api/content/reference/kana/{id}", params);
          break;
        case "kanji":
          await contentApi.PATCH("/api/content/reference/kanji/{id}", params);
          break;
        case "vocab":
          await contentApi.PATCH("/api/content/reference/vocab/{id}", params);
          break;
        case "grammar":
          await contentApi.PATCH("/api/content/reference/grammar/{id}", params);
          break;
        default:
          await contentApi.PATCH("/api/content/reference/sentences/{id}", params);
      }
    },
    onSuccess: invalidate,
  });
}

export function useDeleteReference() {
  const invalidate = useInvalidateContent();
  return useMutation({
    mutationFn: async ({ type, id }: { type: ReferenceType; id: string }) => {
      await contentApi.DELETE("/api/content/reference/{type}/{id}", { params: { path: { type, id } } });
    },
    onSuccess: invalidate,
  });
}
