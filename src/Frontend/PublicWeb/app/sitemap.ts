import type { MetadataRoute } from "next";
export default function sitemap(): MetadataRoute.Sitemap {
  const base = process.env.SITE_URL ?? process.env.PUBLIC_SITE_URL ?? "https://marketplace.localhost";
  return ["", "/gallery", "/artists", "/about", "/contact"].map(path => ({
    url: `${base}${path}`,
    changeFrequency: path === "/gallery" ? "daily" : "weekly",
    priority: path === "" ? 1 : .7,
  }));
}
