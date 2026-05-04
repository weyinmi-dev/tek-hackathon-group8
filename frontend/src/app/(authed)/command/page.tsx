import { redirect } from "next/navigation";

// Handoff design uses /command for the Command Center; the existing app already
// exposes the same screen at /dashboard. Redirect so both URLs work without
// duplicating the page implementation.
export default function CommandRedirect(): never {
  redirect("/dashboard");
}
