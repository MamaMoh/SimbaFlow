import { ChangePasswordPageForm } from "@/components/auth/change-password-page-form";

export default function ChangePasswordPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50">
      <div className="max-w-md w-full space-y-8">
        <div className="text-center">
          <h2 className="mt-6 text-3xl font-bold text-gray-900">
            SimbaFlow
          </h2>
          <p className="mt-2 text-sm text-gray-600">Labour Export Agency</p>
        </div>
        <ChangePasswordPageForm />
      </div>
    </div>
  );
}

