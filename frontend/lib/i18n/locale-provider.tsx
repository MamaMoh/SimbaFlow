"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { LANDING_COPY, type LandingCopy, type LandingLocale } from "@/lib/landing/i18n";

export type AppLocale = LandingLocale;

export const LOCALE_STORAGE_KEY = "simbaflow-locale";

export const APP_CHROME = {
  en: {
    changePassword: "Change Password",
    logOut: "Log out",
    loggingOut: "Logging out...",
    notifications: "Notifications",
    profile: "Profile",
    language: "Language",
    loginTitle: "Sign in",
    loginSubtitle: "Labour Export Agency",
    username: "Username",
    password: "Password",
    usernamePlaceholder: "Enter your username",
    passwordPlaceholder: "Enter your password",
    signIn: "Sign in",
    signingIn: "Signing in...",
    forgotPassword: "Forgot Password?",
    demoHint: "Demo mode: use any username and password (e.g. demo / demo123).",
    backHome: "Back to home",
    nav: {
      Dashboard: "Dashboard",
      Candidates: "Candidates",
      "Workflow Pipeline": "Workflow Pipeline",
      "New Contracts": "New Contracts",
      Embassy: "Embassy",
      LMIS: "LMIS",
      Tickets: "Tickets",
      Departures: "Departures",
      "Departure List": "Departure List",
      "Calendar View": "Calendar View",
      Arrivals: "Arrivals",
      Commissions: "Commissions",
      Finance: "Finance",
      Accounting: "Accounting",
      Reports: "Reports",
      Administration: "Administration",
      "Staff & Users": "Staff & Users",
      Staff: "Staff",
      "Roles & Permissions": "Roles & Permissions",
      Roles: "Roles",
      Offices: "Offices",
      Partners: "Partners",
      Departments: "Departments",
      Tenants: "Tenants",
      "Workflow Config": "Workflow Config",
      Settings: "Settings",
    } as Record<string, string>,
  },
  ar: {
    changePassword: "تغيير كلمة المرور",
    logOut: "تسجيل الخروج",
    loggingOut: "جاري تسجيل الخروج...",
    notifications: "الإشعارات",
    profile: "الملف الشخصي",
    language: "اللغة",
    loginTitle: "تسجيل الدخول",
    loginSubtitle: "وكالة تصدير العمالة",
    username: "اسم المستخدم",
    password: "كلمة المرور",
    usernamePlaceholder: "أدخل اسم المستخدم",
    passwordPlaceholder: "أدخل كلمة المرور",
    signIn: "تسجيل الدخول",
    signingIn: "جاري تسجيل الدخول...",
    forgotPassword: "نسيت كلمة المرور؟",
    demoHint: "وضع العرض: استخدم أي اسم مستخدم وكلمة مرور (مثل demo / demo123).",
    backHome: "العودة للرئيسية",
    nav: {
      Dashboard: "لوحة التحكم",
      Candidates: "المرشحون",
      "Workflow Pipeline": "مسار العمل",
      "New Contracts": "عقود جديدة",
      Embassy: "السفارة",
      LMIS: "LMIS",
      Tickets: "التذاكر",
      Departures: "المغادرات",
      "Departure List": "قائمة المغادرة",
      "Calendar View": "عرض التقويم",
      Arrivals: "الوصول",
      Commissions: "العمولات",
      Finance: "المالية",
      Accounting: "المحاسبة",
      Reports: "التقارير",
      Administration: "الإدارة",
      "Staff & Users": "الموظفون والمستخدمون",
      Staff: "الموظفون",
      "Roles & Permissions": "الأدوار والصلاحيات",
      Roles: "الأدوار",
      Offices: "المكاتب",
      Partners: "الشركاء",
      Departments: "الأقسام",
      Tenants: "المستأجرون",
      "Workflow Config": "إعداد المسار",
      Settings: "الإعدادات",
    } as Record<string, string>,
  },
} as const;

type LocaleContextValue = {
  locale: AppLocale;
  setLocale: (locale: AppLocale) => void;
  dir: "ltr" | "rtl";
  chrome: (typeof APP_CHROME)[AppLocale];
  landing: LandingCopy;
  tNav: (name: string) => string;
};

const LocaleContext = createContext<LocaleContextValue | null>(null);

export function LocaleProvider({ children }: { children: ReactNode }) {
  const [locale, setLocaleState] = useState<AppLocale>("en");

  useEffect(() => {
    try {
      const saved = localStorage.getItem(LOCALE_STORAGE_KEY) as AppLocale | null;
      // migrate old landing-only key
      const legacy = localStorage.getItem("simbaflow-landing-locale") as AppLocale | null;
      const next = saved === "en" || saved === "ar" ? saved : legacy === "ar" ? "ar" : "en";
      setLocaleState(next);
    } catch {
      /* ignore */
    }
  }, []);

  useEffect(() => {
    const root = document.documentElement;
    root.lang = locale;
    root.dir = locale === "ar" ? "rtl" : "ltr";
  }, [locale]);

  const setLocale = useCallback((next: AppLocale) => {
    setLocaleState(next);
    try {
      localStorage.setItem(LOCALE_STORAGE_KEY, next);
      localStorage.removeItem("simbaflow-landing-locale");
    } catch {
      /* ignore */
    }
  }, []);

  const value = useMemo<LocaleContextValue>(() => {
    const chrome = APP_CHROME[locale];
    return {
      locale,
      setLocale,
      dir: locale === "ar" ? "rtl" : "ltr",
      chrome,
      landing: LANDING_COPY[locale],
      tNav: (name: string) => chrome.nav[name] ?? name,
    };
  }, [locale, setLocale]);

  return <LocaleContext.Provider value={value}>{children}</LocaleContext.Provider>;
}

export function useLocale() {
  const ctx = useContext(LocaleContext);
  if (!ctx) {
    throw new Error("useLocale must be used within LocaleProvider");
  }
  return ctx;
}
