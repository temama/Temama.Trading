import { Component, Injector} from '@angular/core';
import { ErrorHandler, Injectable } from '@angular/core';
import { BsModalService } from 'ngx-bootstrap/modal';
import { BsModalRef } from 'ngx-bootstrap/modal/bs-modal-ref.service';

@Component({
  selector: 'app-error-message',
  templateUrl: './error-message.component.html',
  styleUrls: ['./error-message.component.css']
})
export class ErrorModalComponent {
  errMsg: string;

  constructor(public bsModalRef: BsModalRef) { }
}

/** error-message component*/
@Injectable()
export class ErrorMessageComponent implements ErrorHandler {
  bsModalRef: BsModalRef;

  constructor(private injector: Injector) { }
  
  handleError(error) {
    console.log('Hio');

    const initialState = {
      errMsg : (error.message ? error.message : error.toString())
    };
    const bsModal = this.injector.get(BsModalService);
    this.bsModalRef = bsModal.show(ErrorModalComponent, { initialState });

    // IMPORTANT: Rethrow the error otherwise it gets swallowed
    //throw error;
  }
}
