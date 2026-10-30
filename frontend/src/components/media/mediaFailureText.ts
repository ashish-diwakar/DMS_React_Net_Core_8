import { ApiError } from '../../auth/api';

export const mediaFailureText = (error: unknown) =>
  error instanceof ApiError ? error.message : 'The request could not be completed.';
