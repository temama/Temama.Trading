import { Injectable } from "@angular/core";
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot, Router } from "@angular/router";
import { UserService } from "./UserService";


@Injectable()
export class AuthGuard implements CanActivate {

  constructor(private router: Router, private user: UserService) { }

  canActivate(
    next: ActivatedRouteSnapshot,
    state: RouterStateSnapshot): boolean {
    if (this.user.isLoggedIn())
      return true;

    this.router.navigateByUrl('/login');
    return false;
  }
}
