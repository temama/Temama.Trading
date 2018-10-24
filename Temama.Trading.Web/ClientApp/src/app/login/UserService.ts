import { Injectable, Output, EventEmitter } from "@angular/core";
import { HttpHeaders, HttpClient } from "@angular/common/http";
import { Subscriber } from "rxjs/Subscriber";
import { Observable } from "rxjs/Observable";

@Injectable()
export class UserService {
  @Output() loginStateChanged: EventEmitter<any> = new EventEmitter(); 

  constructor(private http: HttpClient) { }

  isLoggedIn(): boolean {
    return !!localStorage.getItem('userToken');
  }

  name(): string {
    return localStorage.getItem('username');
  }

  role(): string {
    return localStorage.getItem('role');
  }

  login(username, password) {
    var reqHeader = new HttpHeaders({ 'Content-Type': 'application/json' });
    return this.http.post('/api/user/login',
      JSON.stringify({ Username: username, Password: password }),
      { headers: reqHeader });
  }

  loginSuccess(loginResponse) {
    localStorage.setItem('userToken', loginResponse.access_token);
    localStorage.setItem('username', loginResponse.username);
    localStorage.setItem('role', loginResponse.role);
    this.loginStateChanged.emit(null);
  }

  logout() {
    localStorage.removeItem('userToken');
    localStorage.removeItem('username');
    localStorage.removeItem('role');
    this.loginStateChanged.emit(null);
  }
}
