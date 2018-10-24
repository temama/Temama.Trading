import { Component, ElementRef, ViewChild, AfterViewInit } from '@angular/core';
import { HttpHeaders, HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { UserService } from './UserService';

@Component({
    selector: 'app-login',
    templateUrl: './login.component.html',
    styleUrls: ['./login.component.css']
})
export class LoginComponent implements AfterViewInit {
  @ViewChild('Username') usernameElement: ElementRef;
  isRequesting: boolean;

  /** login ctor */
  constructor(private user: UserService, private router: Router) {

  }

  ngAfterViewInit(): void {
    if (this.usernameElement)
      this.usernameElement.nativeElement.focus();
  }

  doLogin(username, password) {
    //var data = "username=" + username + "&password=" + password;// + "&grand_type=password";
    this.isRequesting = true;
    this.user.login(username, password).subscribe((data: any) => {
      this.isRequesting = false;
      this.user.loginSuccess(data);
      this.router.navigate(['/']);
      },
        (err: HttpErrorResponse) => {
          this.isRequesting = false;
          throw err.error;
        }
      );
  }
}
