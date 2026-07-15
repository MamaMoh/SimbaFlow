import {
  Fragment,
  ReactNode,
  SetStateAction,
  useEffect,
  useRef,
  useState,
} from "react";
import { Dialog, Transition } from "@headlessui/react";
import clsx from "clsx";

interface SheetProps {
  open: boolean;
  setOpen: React.Dispatch<SetStateAction<boolean>>;
  children: ReactNode;
  size?: string;
  overlayBlur?: string;
  overlayTint?: string;
  corner?: string;
  padding?: string;
  customClass?: string;
  withHeader?: ReactNode;
  withFooter?: ReactNode;
  scrollable?: boolean;
}

export default function SheetForm({
  open,
  setOpen,
  children,
  size = "max-w-md",
  overlayBlur = "backdrop-blur-sm",
  overlayTint = "bg-white/20",
  corner = "rounded-l-2xl",
  padding = "p-6",
  customClass = "",
  withHeader,
  withFooter,
  scrollable
}: SheetProps) {
  const panelRef = useRef(null);
  const [closing, setClosing] = useState(false);

  useEffect(() => {
    if (open) {
      setClosing(false);
    } else {
      setClosing(true);
    }
  }, [open]);

  useEffect(() => {
    function handleEsc(e: KeyboardEvent) {
      if (e.key === "Escape") setOpen(false);
    }
    if (open) document.addEventListener("keydown", handleEsc);
    return () => document.removeEventListener("keydown", handleEsc);
  }, [open, setOpen]);
  return (
    <Transition.Root show={open} as={Fragment}>
      <Dialog
        as="div"
        className="relative z-999"
        onClose={() => setOpen(false)}
        initialFocus={panelRef}
      >
        {/* Overlay */}
        <div
          className={clsx(
            "fixed inset-0 transition-all duration-500 ease-in-out transform",
            closing ? "opacity-0 scale-105" : "opacity-100 scale-100",
            overlayBlur,
            overlayTint
          )}
        >
          <div className="absolute inset-0 overflow-hidden flex justify-end">
            <Transition.Child
              as={Fragment}
              enter="transform transition ease-out duration-500 sm:duration-700"
              enterFrom="translate-x-full opacity-0"
              enterTo="translate-x-0 opacity-100"
              leave="transform transition ease-in duration-500 sm:duration-700"
              leaveFrom="translate-x-0 opacity-100"
              leaveTo="translate-x-full opacity-0"
            >
              <Dialog.Panel
                className={clsx(
                  "pointer-events-auto w-screen h-screen bg-white dark:bg-zinc-900 shadow-2xl flex flex-col",
                  size,
                  corner,
                  customClass
                )}
              >
                {withHeader && (
                  <div className="sticky top-0 z-10 border-b border-gray-200 dark:border-zinc-700 bg-white/80 dark:bg-zinc-900/80 backdrop-blur-sm">
                    {withHeader}
                  </div>
                )}

                <div className={clsx("flex-1 overflow-y-auto custom-scrollbar ", padding)}>
                  {children}
                </div>

                {withFooter && (
                  <div className="sticky bottom-0 z-10 border-t border-gray-200 dark:border-zinc-700 bg-white/80 dark:bg-zinc-900/80 backdrop-blur-sm">
                    {withFooter}
                  </div>
                )}
              </Dialog.Panel>
            </Transition.Child>
          </div>
        </div>
      </Dialog>
    </Transition.Root>
  );
}
