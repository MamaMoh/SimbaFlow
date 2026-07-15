import LoginForm from "@/components/auth/login-form";

export default function LoginPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50">
      <div className="max-w-md w-full space-y-8">
        <div className="text-center">
          <h1 className="mt-6 text-5xl font-bold text-gray-900">
            SimbaFlow
          </h1>
          <p className="mt-3 text-lg text-gray-600 font-medium">
            Labour Export Agency
          </p>
        </div>
        <LoginForm />
      </div>
    </div>
  );
}
