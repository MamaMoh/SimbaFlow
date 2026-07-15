/**
 * Formats a date string consistently for server and client rendering
 * to avoid hydration mismatches
 */
export function formatDate(dateString: string | null | undefined, options?: {
  includeYear?: boolean;
  includeTime?: boolean;
}): string {
  if (!dateString) return "—";
  
  try {
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return "—";
    
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    
    if (options?.includeTime) {
      const hours = String(date.getHours()).padStart(2, '0');
      const minutes = String(date.getMinutes()).padStart(2, '0');
      return `${year}-${month}-${day} ${hours}:${minutes}`;
    }
    
    if (options?.includeYear) {
      return `${year}-${month}-${day}`;
    }
    
    return `${month}/${day}/${year}`;
  } catch {
    return "—";
  }
}

/**
 * Formats a date for display in a readable format (e.g., "Jan 15, 2025")
 * Uses consistent formatting to avoid hydration mismatches
 */
export function formatDateReadable(dateString: string | null | undefined): string {
  if (!dateString) return "—";
  
  try {
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return "—";
    
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    const month = months[date.getMonth()];
    const day = date.getDate();
    const year = date.getFullYear();
    
    return `${month} ${day}, ${year}`;
  } catch {
    return "—";
  }
}

/**
 * Formats a date for short display (e.g., "Jan 15")
 * Uses consistent formatting to avoid hydration mismatches
 */
export function formatDateShort(dateString: string | null | undefined): string {
  if (!dateString) return "—";
  
  try {
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return "—";
    
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    const month = months[date.getMonth()];
    const day = date.getDate();
    
    return `${month} ${day}`;
  } catch {
    return "—";
  }
}

