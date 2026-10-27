import { publicApi } from "@/lib/public-api";

export async function PublicBanner() {
  let banner: { message: string; endsAtUtc?: string } | null;
  try {
    banner = await publicApi<{ message: string; endsAtUtc?: string } | null>("public/content/banner");
  } catch {
    return null;
  }

  if (!banner) return null;
  return <aside role="status" className="bg-[#251b18] px-6 py-3 text-center text-sm font-bold text-white">{banner.message}</aside>;
}
