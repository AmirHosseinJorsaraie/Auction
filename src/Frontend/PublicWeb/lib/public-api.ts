export type GalleryItem = {
  id: string; artworkId: string; title: string; artistAttribution: string; year?: number;
  priceAmount: number; currency: string; status: string; available: boolean;
  category: { name: string; slug: string }; artist?: { name: string; slug: string };
  seller: { displayName: string }; imageAssetId?: string;
};

export type GalleryPage = { items: GalleryItem[]; page: number; pageSize: number; total: number; totalPages: number };

export type ListingDetail = GalleryItem & {
  description: string; medium: string; dimensions: string; condition: string;
  artist?: { name: string; slug: string; biography?: string };
  seller: { displayName: string; bio?: string };
  images: Array<{ id: string; url: string }>;
};

export async function publicApi<T>(path: string): Promise<T> {
  const base = process.env.API_BASE_URL ?? "http://localhost:8080";
  const response = await fetch(new URL(`/api/${path.replace(/^\//, "")}`, base), { cache: "no-store" });
  if (!response.ok) throw new Error(`Public API returned ${response.status}`);
  return response.json() as Promise<T>;
}

export function mediaUrl(assetId?: string): string {
  return assetId ? `/api/public/media/${assetId}` : "/artwork-placeholder.svg";
}

export function formatMoney(amount: number, currency: string): string {
  return new Intl.NumberFormat("fa-IR", { style: "currency", currency, maximumFractionDigits: 0 }).format(amount);
}
