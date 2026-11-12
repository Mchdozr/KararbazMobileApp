// Sunucudaki AccountRules ile aynı kurallar: kullanıcı hatayı göndermeden önce görsün.
export const MIN_PASSWORD = 8;
export const MAX_NAME = 40;

export function emailProblem(email: string): string | null {
  const value = email.trim();
  if (!value) {
    return 'E-postanı yaz.';
  }
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(value)) {
    return 'E-posta adresi eksik görünüyor.';
  }
  return null;
}

export function passwordProblem(password: string): string | null {
  if (password.length < MIN_PASSWORD) {
    return `Şifre en az ${MIN_PASSWORD} karakter olmalı.`;
  }
  if (!/\p{L}/u.test(password) || !/\d/.test(password)) {
    return 'Şifrede en az bir harf ve bir rakam olmalı.';
  }
  return null;
}

export function nameProblem(name: string): string | null {
  const value = name.trim();
  if (!value) {
    return 'Bir ad yaz.';
  }
  if (value.length > MAX_NAME) {
    return `Ad en fazla ${MAX_NAME} karakter olabilir.`;
  }
  return null;
}

/** 0-3: zayıf, idare eder, iyi, güçlü. */
export function passwordStrength(password: string): 0 | 1 | 2 | 3 {
  if (passwordProblem(password)) {
    return 0;
  }
  let score = 1;
  if (password.length >= 12) {
    score += 1;
  }
  if (/[^\p{L}\d]/u.test(password) || (/\p{Lu}/u.test(password) && /\p{Ll}/u.test(password))) {
    score += 1;
  }
  return score as 1 | 2 | 3;
}
