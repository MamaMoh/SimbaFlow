"use client";

import { redirect } from "next/navigation";

/** Legacy path — Offices is the product name for department/branch CRUD. */
export default function DepartmentsRedirectPage() {
  redirect("/offices");
}
