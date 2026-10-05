import { describe, expect, it } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import { toApiError } from '../api/client';

describe('api errors', () => {
  it('maps the API error body', () => {
    const error = new AxiosError('fail', 'ERR', undefined, undefined, {
      status: 422,
      statusText: 'Unprocessable',
      headers: {},
      config: { headers: new AxiosHeaders() },
      data: { success: false, message: 'Pickup must be in the future', errorCode: 'PICKUP_IN_PAST', errors: { requestedPickupDateUtc: ['Too early'] } },
    });
    const mapped = toApiError(error);
    expect(mapped.status).toBe(422);
    expect(mapped.code).toBe('PICKUP_IN_PAST');
    expect(mapped.fieldErrors.requestedPickupDateUtc).toEqual(['Too early']);
  });

  it('explains network failures in plain words', () => {
    expect(toApiError(new AxiosError('Network Error')).message).toMatch(/internet connection/);
  });
});
