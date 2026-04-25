import { redirect } from "next/navigation";
import { cookies } from "next/headers";

export default async function HomePage() {
  const jar = await cookies();
  redirect(jar.get("tp_access")?.value ? "/dashboard" : "/login");
}
