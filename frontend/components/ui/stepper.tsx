import * as React from "react";
import { Check, Circle, Dot } from "lucide-react";
import { cn } from "@/lib/utils";

interface StepperProps {
  currentStep: number;
  className?: string;
  children: React.ReactNode;
}

interface StepperStepProps {
  stepNumber: number;
  title: string;
  description: string;
  currentStep: number;
  isLastStep?: boolean;
}

const Stepper = React.forwardRef<HTMLDivElement, StepperProps>(
  ({ currentStep, className, children, ...props }, ref) => {
    return (
      <div ref={ref} className={cn("w-full", className)} {...props}>
        {children}
      </div>
    );
  }
);
Stepper.displayName = "Stepper";

const StepperStep = React.forwardRef<HTMLDivElement, StepperStepProps>(
  (
    { stepNumber, title, description, currentStep, isLastStep, ...props },
    ref
  ) => {
    const state =
      currentStep > stepNumber
        ? "completed"
        : currentStep === stepNumber
        ? "active"
        : "inactive";

    return (
      <div
        ref={ref}
        className="relative flex w-full flex-col items-center justify-center"
        {...props}
      >
        {/* Separator line */}
        {!isLastStep && (
          <div
            className={cn(
              "absolute left-[calc(50%+20px)] right-[calc(-50%+10px)] top-5 block h-0.5 shrink-0 rounded-full bg-muted transition-colors duration-300",
              state === "completed" && "bg-primary"
            )}
          />
        )}

        {/* Step circle */}
        <div
          className={cn(
            "z-10 flex h-10 w-10 items-center justify-center rounded-full border-2 transition-all duration-300",
            state === "completed" &&
              "border-primary bg-primary text-primary-foreground",
            state === "active" &&
              "border-primary bg-background text-primary ring-2 ring-primary ring-offset-2 ring-offset-background",
            state === "inactive" &&
              "border-muted-foreground/30 bg-background text-muted-foreground"
          )}
        >
          {state === "completed" && <Check className="h-5 w-5" />}
          {state === "active" && <Circle className="h-5 w-5 fill-current" />}
          {state === "inactive" && <Dot className="h-5 w-5" />}
        </div>

        {/* Step content */}
        <div className="mt-3 flex flex-col items-center text-center">
          <div
            className={cn(
              "text-sm font-semibold transition-colors duration-300",
              state === "active" && "text-primary",
              state === "completed" && "text-foreground",
              state === "inactive" && "text-muted-foreground"
            )}
          >
            {title}
          </div>
          <div
            className={cn(
              "text-xs text-muted-foreground transition-colors duration-300 mt-1",
              state === "active" && "text-primary/70"
            )}
          >
            {description}
          </div>
        </div>
      </div>
    );
  }
);
StepperStep.displayName = "StepperStep";

export { Stepper, StepperStep };
