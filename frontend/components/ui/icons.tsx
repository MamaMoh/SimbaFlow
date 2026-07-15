import React from "react"
import Image from "next/image"
import { AvatarIcon, ImageIcon } from "@radix-ui/react-icons"
import { BookText } from "lucide-react"

type IconProps = React.HTMLAttributes<SVGElement>
export const Icons = {
  logo: () => (
    <Image
      src={"/images/et-logo.png"}
      alt="ethiopian airlines logo"
      width={"250"}
      height={"250"}
      className="mx-auto mb-4"
    />
  ),

  spinner: (props: IconProps) => (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      <path d="M21 12a9 9 0 1 1-6.219-8.56" />
    </svg>
  ),
  avatar: AvatarIcon,
  placeholder: ImageIcon,
  emptyIcon: BookText,
}
