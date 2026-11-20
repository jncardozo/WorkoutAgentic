import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { switchMap } from 'rxjs';
import { AuthApi } from '../../core/auth-api';

export interface AgreementDocument {
  version: string;
  title: string;
  text: string;
  hash: string;
  available: boolean;
}

@Injectable({ providedIn: 'root' })
export class AgreementApi {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthApi);

  load() { return this.http.get<AgreementDocument>('/api/legal/agreement'); }

  accept(document: AgreementDocument) {
    return this.auth.csrf().pipe(
      switchMap(() => this.http.post<void>('/api/legal/accept', {
        version: document.version, hash: document.hash, accepted: true,
      })),
      switchMap(() => this.auth.load()),
    );
  }
}
