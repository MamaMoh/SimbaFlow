"use client";

import PhoneInput from "react-phone-input-2";
import "react-phone-input-2/lib/style.css";

interface PhoneInputFieldProps {
  value?: string;
  onChange: (value: string) => void;
  placeholder?: string;
}

export function PhoneInputField({ value, onChange, placeholder = "Enter phone number" }: PhoneInputFieldProps) {
  return (
    <div className="phone-input-wrapper">
      <PhoneInput
        country="et"
        value={value}
        onChange={(phone) => onChange(`+${phone}`)}
        placeholder={placeholder}
        enableSearch
        searchPlaceholder="Search country..."
        inputClass="!w-full !h-9 !text-sm !rounded-md !border !border-input !bg-transparent !pl-12"
        buttonClass="!rounded-l-md !border !border-input !bg-transparent !border-r-0"
        containerClass="!w-full"
        dropdownClass="!rounded-md !shadow-md !border !border-input"
        searchClass="!rounded-sm !border !border-input !text-sm"
      />
    </div>
  );
}
