import { Injectable } from "@angular/core";
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent } from "@angular/common/http";
import { Observable } from "rxjs/Observable";
import { Router } from "@angular/router";
import { UserService } from "./UserService";
import 'rxjs/add/operator/do';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  constructor(private router: Router, private user: UserService) { }

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    var clonedReq = req.clone();

    if (req.headers.get('No-Auth') == 'true')
      return next.handle(clonedReq);

    if (this.user.isLoggedIn()) {
      clonedReq = clonedReq.clone({ headers: clonedReq.headers.set("Authorization", "Bearer " + this.user.token()) });
    }

    return next.handle(clonedReq).do(
      succ => { },
      err => {
        if (err.status == 401)
          //this.router.navigateByUrl('/login');
          this.user.logout();
      });
  }
}
