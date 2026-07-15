/**
 * Password Strength Checker
 * Calculates password strength based on various criteria
 */

export interface PasswordStrength {
  score: number; // 0-100
  percentage: number; // 0-100
  level: 'weak' | 'fair' | 'good' | 'strong' | 'very-strong';
  feedback: string[];
  meetsMinimum: boolean; // true if >= 60%
  meetsRecommended: boolean; // true if >= 65% (recommended but not required)
  meetsApiRequirements: boolean; // true if has a letter and a number
}

/**
 * Calculate password strength
 */
export function calculatePasswordStrength(password: string): PasswordStrength {
  if (!password) {
    return {
      score: 0,
      percentage: 0,
      level: 'weak',
      feedback: [],
      meetsMinimum: false,
      meetsRecommended: false,
      meetsApiRequirements: false,
    };
  }

  let score = 0;
  const feedback: string[] = [];

  // Length checks
  if (password.length >= 8) {
    score += 15;
  } else {
    feedback.push('Use at least 8 characters');
  }

  if (password.length >= 12) {
    score += 10;
  }

  if (password.length >= 16) {
    score += 5;
  }

  // Character variety checks
  const hasLowercase = /[a-z]/.test(password);
  const hasUppercase = /[A-Z]/.test(password);
  const hasNumber = /[0-9]/.test(password);
  const hasSpecial = /[^a-zA-Z0-9]/.test(password);
  const hasLetter = hasLowercase || hasUppercase;

  if (hasLowercase) {
    score += 10;
  } else {
    feedback.push('Add at least one lowercase letter');
  }

  if (hasUppercase) {
    score += 10;
  } else {
    feedback.push('Add at least one uppercase letter');
  }

  if (hasNumber) {
    score += 10;
  } else {
    feedback.push('Add at least one digit');
  }

  if (hasSpecial) {
    score += 15;
  } else {
    feedback.push('Add at least one special character (!@#$%^&*_+-=[]{}|;:,.<>?)');
  }

  // Check if password meets API requirements (letter + number)
  const meetsApiRequirements = hasLetter && hasNumber;

  // Pattern checks (penalties for common patterns)
  if (/(.)\1{2,}/.test(password)) {
    score -= 10; // Repeated characters
    feedback.push('Avoid repeating characters');
  }

  if (/123|abc|qwe|password|admin/i.test(password)) {
    score -= 15; // Common sequences
    feedback.push('Avoid common sequences');
  }

  // Ensure score is between 0 and 100
  score = Math.max(0, Math.min(100, score));

  // Determine level
  let level: PasswordStrength['level'];
  if (score < 30) {
    level = 'weak';
  } else if (score < 50) {
    level = 'fair';
  } else if (score < 75) {
    level = 'good';
  } else if (score < 90) {
    level = 'strong';
  } else {
    level = 'very-strong';
  }

  return {
    score,
    percentage: score,
    level,
    feedback: feedback.length > 0 ? feedback : [],
    meetsMinimum: score >= 60,
    meetsRecommended: score >= 65,
    meetsApiRequirements: meetsApiRequirements,
  };
}

/**
 * Get color for password strength level
 */
export function getPasswordStrengthColor(level: PasswordStrength['level']): string {
  switch (level) {
    case 'weak':
      return 'bg-red-500';
    case 'fair':
      return 'bg-orange-500';
    case 'good':
      return 'bg-yellow-500';
    case 'strong':
      return 'bg-green-500';
    case 'very-strong':
      return 'bg-emerald-600';
    default:
      return 'bg-gray-300';
  }
}

/**
 * Get text color for password strength level
 */
export function getPasswordStrengthTextColor(level: PasswordStrength['level']): string {
  switch (level) {
    case 'weak':
      return 'text-red-600';
    case 'fair':
      return 'text-orange-600';
    case 'good':
      return 'text-yellow-600';
    case 'strong':
      return 'text-green-600';
    case 'very-strong':
      return 'text-emerald-700';
    default:
      return 'text-gray-600';
  }
}

/**
 * Get label for password strength level
 */
export function getPasswordStrengthLabel(level: PasswordStrength['level']): string {
  switch (level) {
    case 'weak':
      return 'Weak';
    case 'fair':
      return 'Fair';
    case 'good':
      return 'Good';
    case 'strong':
      return 'Strong';
    case 'very-strong':
      return 'Very Strong';
    default:
      return 'Unknown';
  }
}

